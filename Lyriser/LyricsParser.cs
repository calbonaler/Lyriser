using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Controls;
using SharpDX.DirectWrite;

namespace Lyriser
{
	public class LyricsParser : IHighlightTokenizer
	{
		public LyricsParser(ErrorSink errorSink) => ErrorSink = errorSink;

		string _line;
		int _baseIndex;
		int _lineIndex;

		bool Accept(char ch)
		{
			if (Predict(ch))
			{
				_lineIndex++;
				return true;
			}
			return false;
		}

		bool Predict(char ch) => _lineIndex < _line.Length && _line[_lineIndex] == ch;

		static string ReadLineWithLength(TextReader reader, out int length)
		{
			var sb = new StringBuilder();
			length = 0;
			int ch;
			while (true)
			{
				ch = reader.Read();
				if (ch == -1)
				{
					if (sb.Length > 0)
						return sb.ToString();
					return null;
				}
				length++;
				if (ch == 13 && reader.Peek() == 10)
				{
					reader.Read();
					length++;
				}
				if (ch == 13 || ch == 10)
					return sb.ToString();
				sb.Append((char)ch);
			}
		}

		int Index => _baseIndex + _lineIndex;

		public ErrorSink ErrorSink { get; }

		public IEnumerable<(RubiedLine Line, CharacterIndex[][] Keys)> Transform(string source)
		{
			using (var reader = new StringReader(source))
			{
				var lineIndex = 0;
				foreach (var nodes in Parse(reader))
				{
					var baseTextBuilder = new StringBuilder();
					var rubySpecs = new List<RubySpecifier>();
					var keyStore = new KeyStore();
					foreach (var node in nodes)
						node.Transform(lineIndex, baseTextBuilder, rubySpecs, keyStore);
					yield return (new RubiedLine(baseTextBuilder.ToString(), rubySpecs.ToArray()), keyStore.ToArray());
					lineIndex++;
				}
			}
		}

		IEnumerable<LyricsNode[]> Parse(TextReader reader)
		{
			ErrorSink.Clear();
			_baseIndex = 0;
			while ((_line = ReadLineWithLength(reader, out var len)) != null)
			{
				yield return ParseLyricsLine();
				_baseIndex += len;
			}
		}

		LyricsNode[] ParseLyricsLine()
		{
			var items = new List<LyricsNode>();
			_lineIndex = 0;
			while (_lineIndex < _line.Length)
				items.Add(ParseLyricsItem());
			return items.ToArray();
		}

		LyricsNode ParseLyricsItem()
		{
			var start = Index;
			if (Accept('('))
			{
				var items = new List<LyricsNode>();
				while (!Accept(')') && _lineIndex < _line.Length)
					items.Add(ParseLyricsItem());
				return new SkippedNode(items, start, Index - start);
			}
			if (Accept('['))
			{
				var mainItems = new List<SimpleNode>();
				while (!Predict('"'))
				{
					if (_lineIndex >= _line.Length)
					{
						ErrorSink.ReportError("文字 '\"' が予期されましたが、行終端記号が見つかりました。", Index);
						break;
					}
					mainItems.Add(ParseSimpleItem());
				}
				if (mainItems.Count <= 0)
				{
					ErrorSink.ReportError("ふりがなのベースを空にすることはできません。", Index);
					mainItems.Add(new SimpleNode("_", CharacterState.Default, Index, 0));
				}
				var items = new List<SimpleNode>();
				if (Accept('"'))
				{
					while (!Accept('"'))
					{
						if (_lineIndex >= _line.Length)
						{
							ErrorSink.ReportError("文字 '\"' が予期されましたが、行終端記号が見つかりました。", Index);
							break;
						}
						items.Add(ParseSimpleItem());
					}
				}
				if (!Accept(']'))
				{
					if (_lineIndex < _line.Length)
						ErrorSink.ReportError(string.Format(CultureInfo.CurrentCulture, "文字 ']' が予期されましたが、文字 '{0}' が見つかりました。", _line[_lineIndex]), Index);
					else
						ErrorSink.ReportError("文字 ']' が予期されましたが、行終端記号が見つかりました。", Index);
				}
				return new CompositeNode(mainItems, items, start, Index - start);
			}
			var item = ParseSimpleItem();
			if (!Accept('"'))
				return item;
			var phoneticItems = new List<SimpleNode>();
			while (!Accept('"'))
			{
				if (_lineIndex >= _line.Length)
				{
					ErrorSink.ReportError("文字 '\"' が予期されましたが、行終端記号が見つかりました。", Index);
					break;
				}
				phoneticItems.Add(ParseSimpleItem());
			}
			return new CompositeNode(new[] { item }, phoneticItems, start, Index - start);
		}

		SimpleNode ParseSimpleItem()
		{
			var start = Index;
			var escaping = false;
			if (Accept('`'))
				escaping = true;
			var sb = new StringBuilder();
			if (_lineIndex < _line.Length)
				sb.Append(_line[_lineIndex++]);
			else
			{
				ErrorSink.ReportError("文字が予期されましたが、行終端記号が見つかりました。", Index);
				sb.Append('_');
			}
			if (char.IsHighSurrogate(sb[sb.Length - 1]) && _lineIndex < _line.Length && char.IsLowSurrogate(_line[_lineIndex]))
				sb.Append(_line[_lineIndex++]);
			var text = sb.ToString();
			var state = CharacterState.Default;
			if (!escaping)
			{
				if (text == "{")
					state = CharacterState.StartGrouping;
				else if (text == "}")
					state = CharacterState.StopGrouping;
			}
			return new SimpleNode(text, state, start, Index - start);
		}

		[CLSCompliant(false)]
		public IEnumerable<HighlightToken> GetTokens(string text)
		{
			using (var reader = new StringReader(text))
				return Parse(reader).SelectMany(x => x.SelectMany(y => y.Tokens)).ToArray();
		}
	}

	class KeyStore
	{
		readonly List<CharacterIndex[]> _keys = new List<CharacterIndex[]>();
		List<CharacterIndex> _subKeys;

		public void StartGrouping()
		{
			if (_subKeys == null)
				_subKeys = new List<CharacterIndex>();
		}

		public void StopGrouping()
		{
			if (_subKeys != null)
			{
				_keys.Add(_subKeys.ToArray());
				_subKeys = null;
			}
		}

		public void Add(CharacterIndex index)
		{
			if (_subKeys != null)
				_subKeys.Add(index);
			else
				_keys.Add(new[] { index });
		}

		public CharacterIndex[][] ToArray() => _keys.ToArray();
	}

	abstract class LyricsNode
	{
		protected LyricsNode(int start, int length)
		{
			StartIndex = start;
			Length = length;
		}

		public abstract void Transform(int lineIndex, StringBuilder textBuilder, List<RubySpecifier> rubySpecifiers, KeyStore keyStore);

		public abstract IEnumerable<HighlightToken> Tokens { get; }

		public int StartIndex { get; }

		public int Length { get; }
	}

	class SimpleNode : LyricsNode
	{
		public SimpleNode(string text, CharacterState state, int start, int length) : base(start, length)
		{
			_state = state;
			_text = text;
		}

		readonly CharacterState _state;
		readonly string _text;

		public string Text => _state == CharacterState.Default ? _text : string.Empty;

		public override void Transform(int lineIndex, StringBuilder textBuilder, List<RubySpecifier> rubySpecifiers, KeyStore keyStore) => Transform(lineIndex, textBuilder, -1, keyStore);

		public void Transform(int lineIndex, StringBuilder textBuilder, int rubyIndex, KeyStore keyStore)
		{
			if (_state == CharacterState.StartGrouping)
				keyStore?.StartGrouping();
			else if (_state == CharacterState.StopGrouping)
				keyStore?.StopGrouping();
			else
			{
				if (keyStore != null && !string.IsNullOrWhiteSpace(Text))
					keyStore.Add(new CharacterIndex(lineIndex, rubyIndex, textBuilder.Length));
				textBuilder.Append(Text);
			}
		}

		public override IEnumerable<HighlightToken> Tokens
		{
			get
			{
				if (_state == CharacterState.StartGrouping || _state == CharacterState.StopGrouping)
					yield return new HighlightToken(StartIndex, Length, Color.Red, Color.Empty);
			}
		}

		public override string ToString() => _state == CharacterState.Default ? _text : "(" + _state.ToString() + ")";
	}

	class SkippedNode : LyricsNode
	{
		public SkippedNode(IEnumerable<LyricsNode> items, int start, int length) : base(start, length) => _items = items.ToArray();

		readonly LyricsNode[] _items;

		public override void Transform(int lineIndex, StringBuilder textBuilder, List<RubySpecifier> rubySpecifiers, KeyStore keyStore)
		{
			foreach (var item in _items)
				item.Transform(lineIndex, textBuilder, rubySpecifiers, null);
		}

		public override IEnumerable<HighlightToken> Tokens => Enumerable.Repeat(new HighlightToken(StartIndex, Length, Color.Green, Color.Empty), 1);
	}

	class CompositeNode : LyricsNode
	{
		public CompositeNode(IEnumerable<SimpleNode> rawText, IEnumerable<SimpleNode> phonetic, int start, int length) : base(start, length)
		{
			_text = string.Concat(rawText.Select(x => x.Text));
			RawTextStartIndex = rawText.First().StartIndex;
			_rawTextLength = rawText.Sum(x => x.Length);
			_ruby = phonetic.ToArray();
		}

		readonly string _text;
		readonly int _rawTextLength;
		readonly SimpleNode[] _ruby;

		public override void Transform(int lineIndex, StringBuilder textBuilder, List<RubySpecifier> rubySpecifiers, KeyStore keyStore)
		{
			var rubyTextBuilder = new StringBuilder();
			foreach (var rubyNode in _ruby)
				rubyNode.Transform(lineIndex, rubyTextBuilder, rubySpecifiers.Count, keyStore);
			rubySpecifiers.Add(new RubySpecifier(new TextRange(textBuilder.Length, _text.Length), rubyTextBuilder.ToString()));
			textBuilder.Append(_text);
		}

		public override IEnumerable<HighlightToken> Tokens
		{
			get
			{
				yield return new HighlightToken(RawTextStartIndex, _rawTextLength, Color.Red, Color.Empty);
				foreach (var phonetic in _ruby)
				{
					var phtokens = phonetic.Tokens.ToArray();
					if (phtokens.Length > 0)
					{
						foreach (var token in phtokens)
							yield return token;
					}
					else
						yield return new HighlightToken(phonetic.StartIndex, phonetic.Length, Color.Blue, Color.Empty);
				}
			}
		}

		public int RawTextStartIndex { get; }

		public override string ToString() => _text + "(" + string.Concat(_ruby.Select(x => x.Text)) + ")";
	}

	enum CharacterState
	{
		Default,
		StartGrouping,
		StopGrouping,
	}
}
