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

		public IEnumerable<LyricsLine> Analyze(string source)
		{
			using (var reader = new StringReader(source))
			{
				foreach (var line in Parse(reader))
				{
					AnalyzerSink sink = new AnalyzerSink();
					StringBuilder sb = new StringBuilder();
					LyricsItem.AnalyzeAll(line, sb, sink, true);
					yield return new LyricsLine(sb.ToString(), sink.Syllables, sink.Phonetics);
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
						tokens.AddRange(item.GetTokens());
				}
			}
			return tokens;
		}

		class AnalyzerSink
		{
			public AnalyzerSink()
			{
				Syllables = new List<Syllable>();
				Phonetics = new Dictionary<int, Phonetic>();
			}

			public CharacterPointer SyllableStartIndex { get; set; }

			public List<Syllable> Syllables { get; private set; }

			public Dictionary<int, Phonetic> Phonetics { get; private set; }
		}

		abstract class LyricsItem
		{
			protected LyricsItem(int start, int length)
			{
				StartIndex = start;
				Length = length;
			}

			public static void AnalyzeAll(IEnumerable<LyricsItem> items, StringBuilder sb, AnalyzerSink sink, bool createSyllable)
			{
				foreach (var item in items)
					item.Analyze(sink, sb, () => new CharacterPointer(sb.Length, 0), createSyllable);
			}

			public abstract void Analyze(AnalyzerSink sink, StringBuilder sb, Func<CharacterPointer> pointerProvider, bool createSyllable);

			public abstract IEnumerable<HighlightToken> GetTokens();

			public int StartIndex { get; private set; }

			public int Length { get; private set; }
		}

		class SimpleItem : LyricsItem
		{
			public SimpleItem(string text, CharacterState state, int start, int length)
				: base(start, length)
			{
				State = state;
				Text = text;
			}

			public CharacterState State { get; private set; }

			public string Text { get; private set; }

			public override void Analyze(AnalyzerSink sink, StringBuilder sb, Func<CharacterPointer> pointerProvider, bool createSyllable)
			{
				var beforeText = pointerProvider();
				if (createSyllable)
				{
					if (State == CharacterState.StartGrouping)
						sink.SyllableStartIndex = beforeText;
					else if (State == CharacterState.StopGrouping)
					{
						if (sink.SyllableStartIndex != null)
						{
							sink.Syllables.Add(new Syllable(sink.SyllableStartIndex, beforeText));
							sink.SyllableStartIndex = null;
						}
					}
				}
				if (State == CharacterState.Default)
				{
					sb.Append(Text);
					if (createSyllable && sink.SyllableStartIndex == null && !string.IsNullOrWhiteSpace(Text))
						sink.Syllables.Add(new Syllable(beforeText, pointerProvider()));
				}
			}

			public override IEnumerable<HighlightToken> GetTokens()
			{
				if (State == CharacterState.StartGrouping || State == CharacterState.StopGrouping)
					yield return new HighlightToken(StartIndex, Length, Color.Red, Color.Empty);
			}

			public override string ToString() { return Text + ", " + State.ToString(); }
		}

		class SkippedItem : LyricsItem
		{
			public SkippedItem(LyricsItem[] items, int start, int length) : base(start, length) { Items = items; }

			public LyricsItem[] Items { get; private set; }

			public override void Analyze(AnalyzerSink sink, StringBuilder sb, Func<CharacterPointer> pointerProvider, bool createSyllable) { AnalyzeAll(Items, sb, sink, false); }

			public override IEnumerable<HighlightToken> GetTokens() { yield return new HighlightToken(StartIndex, Length, Color.Green, Color.Empty); }
		}

		class CompositeItem : LyricsItem
		{
			public CompositeItem(SimpleItem[] rawText, SimpleItem[] phonetic, int start, int length)
				: base(start, length)
			{
				_rawText = rawText;
				Phonetic = phonetic;
			}

			SimpleItem[] _rawText;

			public string Text { get { return string.Concat(_rawText.Where(x => x.State == CharacterState.Default).Select(x => x.Text)); } }

			public SimpleItem[] Phonetic { get; private set; }

			public override void Analyze(AnalyzerSink sink, StringBuilder sb, Func<CharacterPointer> pointerProvider, bool createSyllable)
			{
				StringBuilder phoneticText = new StringBuilder();
				foreach (var phonetic in Phonetic)
					phonetic.Analyze(sink, phoneticText, () => new CharacterPointer(sb.Length, phoneticText.Length), createSyllable);
				sink.Phonetics.Add(sb.Length, new Phonetic(Text.Length, phoneticText.ToString()));
				sb.Append(Text);
			}

			public override IEnumerable<HighlightToken> GetTokens()
			{
				yield return new HighlightToken(_rawText[0].StartIndex, _rawText.Sum(x => x.Length), Color.Red, Color.Empty);
				foreach (var phonetic in Phonetic)
				{
					var phtokens = phonetic.GetTokens().ToArray();
					if (phtokens.Length > 0)
					{
						foreach (var token in phtokens)
							yield return token;
					}
					else
						yield return new HighlightToken(phonetic.StartIndex, phonetic.Length, Color.Blue, Color.Empty);
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
}
