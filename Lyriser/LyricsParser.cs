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
			if (_lineIndex < _line.Length && _line[_lineIndex] == ch)
			{
				_lineIndex++;
				return true;
			}
			return false;
		}

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

		int Index { get { return _baseIndex + _lineIndex; } }

		public ErrorSink ErrorSink { get; private set; }

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

		IEnumerable<IList<LyricsItem>> Parse(TextReader reader)
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

		IList<LyricsItem> ParseLyricsLine()
		{
			List<LyricsItem> items = new List<LyricsItem>();
			_lineIndex = 0;
			while (_lineIndex < _line.Length)
				items.Add(ParseLyricsItem());
			return items;
		}

		LyricsItem ParseLyricsItem()
		{
			int start = Index;
			if (Accept('('))
			{
				List<LyricsItem> items = new List<LyricsItem>();
				do
				{
					items.Add(ParseLyricsItem());
				} while (!Accept(')') && _lineIndex < _line.Length);
				return new SkippedItem(items.ToArray(), start, Index - start);
			}
			if (Accept('['))
			{
				List<SimpleItem> mainItems = new List<SimpleItem>();
				while (true)
				{
					mainItems.Add(ParseSimpleItem());
					if (Accept('"'))
						break;
					if (_lineIndex >= _line.Length)
					{
						ErrorSink.ReportError("文字 '\"' が予期されましたが、行終端記号が見つかりました。", Index);
						break;
					}
				}
				List<SimpleItem> items = new List<SimpleItem>();
				while (true)
				{
					items.Add(ParseSimpleItem());
					if (Accept('"'))
						break;
					if (_lineIndex >= _line.Length)
					{
						ErrorSink.ReportError("文字 '\"' が予期されましたが、行終端記号が見つかりました。", Index);
						break;
					}
				}
				if (!Accept(']'))
				{
					if (_lineIndex < _line.Length)
						ErrorSink.ReportError(string.Format(CultureInfo.CurrentCulture, "文字 ']' が予期されましたが、文字 '{0}' が見つかりました。", _line[_lineIndex]), Index);
					else
						ErrorSink.ReportError("文字 ']' が予期されましたが、行終端記号が見つかりました。", Index);
				}
				return new CompositeItem(mainItems.ToArray(), items.ToArray(), start, Index - start);
			}
			else
			{
				var item = ParseSimpleItem();
				if (Accept('"'))
				{
					List<SimpleItem> items = new List<SimpleItem>();
					while (true)
					{
						items.Add(ParseSimpleItem());
						if (Accept('"'))
							break;
						if (_lineIndex >= _line.Length)
						{
							ErrorSink.ReportError("文字 '\"' が予期されましたが、行終端記号が見つかりました。", Index);
							break;
						}
					}
					return new CompositeItem(new[] { item }, items.ToArray(), start, Index - start);
				}
				else
					return item;
			}
		}

		SimpleItem ParseSimpleItem()
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
			return new SimpleItem(text, state, start, Index - start);
		}

		public IEnumerable<HighlightToken> GetTokens(string text)
		{
			List<HighlightToken> tokens = new List<HighlightToken>();
			using (var reader = new StringReader(text))
			{
				foreach (var line in Parse(reader))
				{
					foreach (var item in line)
						tokens.AddRange(item.Tokens);
				}
			}
			return tokens;
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

		public int SyllableCount { get { return _syllableId; } }
	}

	abstract class LyricsItem
	{
		protected LyricsItem(int start, int length)
		{
			StartIndex = start;
			Length = length;
		}

		public abstract IEnumerable<LyricsSection> Transform(SyllableIdProvider provider);

		public abstract IEnumerable<HighlightToken> Tokens { get; }

		public int StartIndex { get; private set; }

		public int Length { get; private set; }
	}

	class SimpleItem : LyricsItem
	{
		public SimpleItem(string text, CharacterState state, int start, int length) : base(start, length)
		{
			State = state;
			Text = text;
		}

		public CharacterState State { get; private set; }

		public string Text { get; private set; }

		public override IEnumerable<LyricsSection> Transform(SyllableIdProvider provider)
		{
			if (State == CharacterState.StartGrouping)
			{
				if (provider != null)
					provider.StartSyllableGeneration();
				return Enumerable.Empty<LyricsSection>();
			}
			else if (State == CharacterState.StopGrouping)
			{
				if (provider != null)
					provider.StopSyllableGeneration();
				return Enumerable.Empty<LyricsSection>();
			}
			else
			{
				if (provider != null && !string.IsNullOrWhiteSpace(Text))
					return Enumerable.Repeat(new LyricsCharacterSection(Text, provider.GetOrUpdateSyllableId()), 1);
				else
					return Enumerable.Repeat(new LyricsCharacterSection(Text, null), 1);
			}
		}

		public override IEnumerable<HighlightToken> Tokens
		{
			get
			{
				if (State == CharacterState.StartGrouping || State == CharacterState.StopGrouping)
					yield return new HighlightToken(StartIndex, Length, Color.Red, Color.Empty);
			}
		}

		public override string ToString() { return Text + ", " + State.ToString(); }
	}

	class SkippedItem : LyricsItem
	{
		public SkippedItem(LyricsItem[] items, int start, int length) : base(start, length) { Items = items; }

		public LyricsItem[] Items { get; private set; }

		public override IEnumerable<LyricsSection> Transform(SyllableIdProvider provider) { return Items.SelectMany(x => x.Transform(null)); }

		public override IEnumerable<HighlightToken> Tokens { get { yield return new HighlightToken(StartIndex, Length, Color.Green, Color.Empty); } }
	}

	class CompositeItem : LyricsItem
	{
		public CompositeItem(SimpleItem[] rawText, SimpleItem[] phonetic, int start, int length) : base(start, length)
		{
			_rawText = rawText;
			Phonetic = phonetic;
		}

		SimpleItem[] _rawText;

		public string Text { get { return string.Concat(_rawText.Where(x => x.State == CharacterState.Default).Select(x => x.Text)); } }

		public SimpleItem[] Phonetic { get; private set; }

		public override IEnumerable<LyricsSection> Transform(SyllableIdProvider provider) { return Enumerable.Repeat(new LyricsCompositeSection(Text, Phonetic.SelectMany(x => x.Transform(provider)).Cast<LyricsCharacterSection>()), 1); }

		public override IEnumerable<HighlightToken> Tokens
		{
			get
			{
				yield return new HighlightToken(_rawText[0].StartIndex, _rawText.Sum(x => x.Length), Color.Red, Color.Empty);
				foreach (var phonetic in Phonetic)
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

		public override string ToString() { return Text + "(" + string.Concat(Phonetic.Where(x => x.State == CharacterState.Default).Select(x => x.Text)) + ")"; }
	}

	enum CharacterState
	{
		Default,
		StartGrouping,
		StopGrouping,
	}
}
