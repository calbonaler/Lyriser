using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Controls;

namespace Lyriser
{
	public class LyricsParser : IHighlightTokenizer
	{
		public LyricsParser(ErrorSink errorSink) { ErrorSink = errorSink; }

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
			StringBuilder sb = new StringBuilder();
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

		public IEnumerable<LyricsLine> Transform(string source)
		{
			using (var reader = new StringReader(source))
			{
				foreach (var line in Parse(reader))
				{
					var provider = new SyllableIdProvider();
					yield return new LyricsLine(line.SelectMany(x => x.Transform(provider)), provider);
				}
			}
		}

		IEnumerable<LyricsNode[]> Parse(TextReader reader)
		{
			int len;
			ErrorSink.Clear();
			_baseIndex = 0;
			while ((_line = ReadLineWithLength(reader, out len)) != null)
			{
				yield return ParseLyricsLine();
				_baseIndex += len;
			}
		}

		LyricsNode[] ParseLyricsLine()
		{
			List<LyricsNode> items = new List<LyricsNode>();
			_lineIndex = 0;
			while (_lineIndex < _line.Length)
				items.Add(ParseLyricsItem());
			return items.ToArray();
		}

		LyricsNode ParseLyricsItem()
		{
			int start = Index;
			if (Accept('('))
			{
				List<LyricsNode> items = new List<LyricsNode>();
				while (!Accept(')') && _lineIndex < _line.Length)
					items.Add(ParseLyricsItem());
				return new SkippedNode(items, start, Index - start);
			}
			if (Accept('['))
			{
				List<SimpleNode> mainItems = new List<SimpleNode>();
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
				List<SimpleNode> items = new List<SimpleNode>();
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
			List<SimpleNode> phoneticItems = new List<SimpleNode>();
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
			int start = Index;
			bool escaping = false;
			if (Accept('`'))
				escaping = true;
			StringBuilder sb = new StringBuilder();
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
			CharacterState state = CharacterState.Default;
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

	class SyllableIdProvider
	{
		bool _syllableIdCreated = false;
		int _syllableId = 0;

		public void StartSyllableGeneration()
		{
			if (!_syllableIdCreated)
				_syllableIdCreated = true;
		}

		public void StopSyllableGeneration()
		{
			if (_syllableIdCreated)
			{
				_syllableIdCreated = false;
				_syllableId++;
			}
		}

		public int GetOrUpdateSyllableId()
		{
			if (!_syllableIdCreated)
				return _syllableId++;
			else
				return _syllableId;
		}

		public int SyllableCount => _syllableId;
	}

	abstract class LyricsNode
	{
		protected LyricsNode(int start, int length)
		{
			StartIndex = start;
			Length = length;
		}

		public abstract IEnumerable<LyricsItem> Transform(SyllableIdProvider provider);

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

		CharacterState _state;
		string _text;

		public string Text => _state == CharacterState.Default ? _text : string.Empty;

		public override IEnumerable<LyricsItem> Transform(SyllableIdProvider provider)
		{
			if (_state == CharacterState.StartGrouping)
			{
				if (provider != null)
					provider.StartSyllableGeneration();
				return Enumerable.Empty<LyricsItem>();
			}
			if (_state == CharacterState.StopGrouping)
			{
				if (provider != null)
					provider.StopSyllableGeneration();
				return Enumerable.Empty<LyricsItem>();
			}
			if (provider != null && !string.IsNullOrWhiteSpace(_text))
				return Enumerable.Repeat(new LyricsCharacterItem(_text, provider.GetOrUpdateSyllableId()), 1);
			else
				return Enumerable.Repeat(new LyricsCharacterItem(_text, null), 1);
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
		public SkippedNode(IEnumerable<LyricsNode> items, int start, int length) : base(start, length) { _items = items.ToArray(); }

		LyricsNode[] _items;

		public override IEnumerable<LyricsItem> Transform(SyllableIdProvider provider) => _items.SelectMany(x => x.Transform(null));

		public override IEnumerable<HighlightToken> Tokens => Enumerable.Repeat(new HighlightToken(StartIndex, Length, Color.Green, Color.Empty), 1);
	}

	class CompositeNode : LyricsNode
	{
		public CompositeNode(IEnumerable<SimpleNode> rawText, IEnumerable<SimpleNode> phonetic, int start, int length) : base(start, length)
		{
			_text = string.Concat(rawText.Select(x => x.Text));
			_rawTextStartIndex = rawText.First().StartIndex;
			_rawTextLength = rawText.Sum(x => x.Length);
			_phonetic = phonetic.ToArray();
		}

		string _text;
		int _rawTextStartIndex;
		int _rawTextLength;
		SimpleNode[] _phonetic;

		public override IEnumerable<LyricsItem> Transform(SyllableIdProvider provider) => Enumerable.Repeat(new LyricsCompositeItem(_text, _phonetic.SelectMany(x => x.Transform(provider)).Cast<LyricsCharacterItem>()), 1);

		public override IEnumerable<HighlightToken> Tokens
		{
			get
			{
				yield return new HighlightToken(_rawTextStartIndex, _rawTextLength, Color.Red, Color.Empty);
				foreach (var phonetic in _phonetic)
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

		public override string ToString() => _text + "(" + string.Concat(_phonetic.Select(x => x.Text)) + ")";
	}

	enum CharacterState
	{
		Default,
		StartGrouping,
		StopGrouping,
	}
}
