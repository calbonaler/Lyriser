using System;
using System.Collections.Generic;
using System.Drawing;
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
			var nodes = new List<LyricsNode>();
			_lineIndex = 0;
			while (_lineIndex < _line.Length)
				nodes.Add(ParseLyricsNode());
			return nodes.ToArray();
		}

		LyricsNode ParseLyricsNode()
		{
			var start = Index;
			if (Accept('('))
			{
				var nodes = new List<LyricsNode>();
				while (!Accept(')'))
				{
					if (_lineIndex >= _line.Length)
					{
						ErrorSink.ReportError("非発音領域が適切に終了されていません。", Index);
						break;
					}
					nodes.Add(ParseLyricsNode());
				}
				return new SkippedNode(nodes, start, Index - start);
			}
			if (Accept('['))
			{
				var mainNodes = new List<SimpleNode>();
				while (!Predict('"'))
				{
					if (_lineIndex >= _line.Length)
					{
						ErrorSink.ReportError("ルビ領域でルビを省略することはできません。", Index);
						break;
					}
					mainNodes.Add(ParseSimpleNode());
				}
				if (mainNodes.Count <= 0)
				{
					ErrorSink.ReportError("ルビ領域でルビを振る対象を省略することはできません。", Index);
					mainNodes.Add(new SimpleNode("_", CharacterState.Default, Index, 0));
				}
				var rubyNodes = new List<SimpleNode>();
				var rubyStartIndex = Index;
				if (Accept('"'))
				{
					rubyStartIndex = Index;
					while (!Accept('"'))
					{
						if (_lineIndex >= _line.Length)
						{
							ErrorSink.ReportError("ルビが適切に終了されていません。", Index);
							break;
						}
						rubyNodes.Add(ParseSimpleNode());
					}
				}
				if (!Accept(']'))
					ErrorSink.ReportError("ルビ領域が適切に終了されていません。", Index);
				ReportErrorIfRubyIsEmptyOrContainsOnlyWhitespaces(rubyNodes, rubyStartIndex);
				return new CompositeNode(mainNodes, rubyNodes, start, Index - start);
			}
			var node = ParseSimpleNode();
			if (Accept('"'))
			{
				var rubyStartIndex = Index;
				var rubyNodes = new List<SimpleNode>();
				while (!Accept('"'))
				{
					if (_lineIndex >= _line.Length)
					{
						ErrorSink.ReportError("ルビが適切に終了されていません。", Index);
						break;
					}
					rubyNodes.Add(ParseSimpleNode());
				}
				ReportErrorIfRubyIsEmptyOrContainsOnlyWhitespaces(rubyNodes, rubyStartIndex);
				return new CompositeNode(new[] { node }, rubyNodes, start, Index - start);
			}
			return node;
		}

		void ReportErrorIfRubyIsEmptyOrContainsOnlyWhitespaces(List<SimpleNode> rubyNodes, int rubyStartIndex)
		{
			if (rubyNodes.Count <= 0)
			{
				ErrorSink.ReportError("ルビを省略することはできません。", rubyStartIndex);
				rubyNodes.Add(new SimpleNode("_", CharacterState.Default, rubyStartIndex, 0));
			}
			else if (rubyNodes.All(x => string.IsNullOrWhiteSpace(x.Text)))
			{
				ErrorSink.ReportError("ルビを空白文字のみとすることはできません。", rubyStartIndex);
				rubyNodes.Add(new SimpleNode("_", CharacterState.Default, rubyNodes[rubyNodes.Count - 1].StartIndex + rubyNodes[rubyNodes.Count - 1].Length, 0));
			}
		}

		SimpleNode ParseSimpleNode()
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
				ErrorSink.ReportError("何らかの文字が必要です。", Index);
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
		public SkippedNode(IEnumerable<LyricsNode> nodes, int start, int length) : base(start, length) => _nodes = nodes.ToArray();

		readonly LyricsNode[] _nodes;

		public override void Transform(int lineIndex, StringBuilder textBuilder, List<RubySpecifier> rubySpecifiers, KeyStore keyStore)
		{
			foreach (var node in _nodes)
				node.Transform(lineIndex, textBuilder, rubySpecifiers, null);
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
