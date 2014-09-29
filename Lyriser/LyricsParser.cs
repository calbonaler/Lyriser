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

		bool Accept(string chars)
		{
			if (_line.Length - _lineIndex < chars.Length)
				return false;
			for (int i = _lineIndex, j = 0; i < _line.Length && j < chars.Length; i++, j++)
			{
				if (_line[i] != chars[j])
					return false;
			}
			_lineIndex += chars.Length;
			return true;
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

		IList<IList<LyricsItem>> Parse(TextReader reader)
		{
			int len;
			List<IList<LyricsItem>> lines = new List<IList<LyricsItem>>();
			ErrorSink.Clear();
			_baseIndex = 0;
			while ((_line = ReadLineWithLength(reader, out len)) != null)
			{
				lines.Add(ParseLyricsLine());
				_baseIndex += len;
			}
			return lines;
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
			if (Accept("("))
			{
				List<LyricsItem> items = new List<LyricsItem>();
				do
				{
					items.Add(ParseLyricsItem());
				} while (!Accept(")") && _lineIndex < _line.Length);
				return new DeletedItem(items.ToArray(), start, Index);
			}
			if (Accept("["))
			{
				List<SimpleItem> mainItems = new List<SimpleItem>();
				while (true)
				{
					mainItems.Add(ParseSimpleItem());
					if (Accept("\""))
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
					if (Accept("\""))
						break;
					if (_lineIndex >= _line.Length)
					{
						ErrorSink.ReportError("文字 '\"' が予期されましたが、行終端記号が見つかりました。", Index);
						break;
					}
				}
				if (!Accept("]"))
				{
					if (_lineIndex < _line.Length)
						ErrorSink.ReportError(string.Format(CultureInfo.CurrentCulture, "文字 ']' が予期されましたが、文字 '{0}' が見つかりました。", _line[_lineIndex]), Index);
					else
						ErrorSink.ReportError("文字 ']' が予期されましたが、行終端記号が見つかりました。", Index);
				}
				return new CompositeItem(mainItems.ToArray(), items.ToArray(), start, Index);
			}
			else
			{
				var item = ParseSimpleItem();
				if (Accept("\""))
				{
					List<SimpleItem> items = new List<SimpleItem>();
					while (true)
					{
						items.Add(ParseSimpleItem());
						if (Accept("\""))
							break;
						if (_lineIndex >= _line.Length)
						{
							ErrorSink.ReportError("文字 '\"' が予期されましたが、行終端記号が見つかりました。", Index);
							break;
						}
					}
					return new CompositeItem(new[] { item }, items.ToArray(), start, Index);
				}
				else
					return item;
			}
		}

		SimpleItem ParseSimpleItem()
		{
			int start = Index;
			bool escaping = false;
			if (Accept("`"))
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
			return new SimpleItem(text, state, start, Index);
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

			public Tuple<int, int> SyllableStartIndex { get; set; }

			public List<Syllable> Syllables { get; private set; }

			public Dictionary<int, Phonetic> Phonetics { get; private set; }

			public int Subtract(Tuple<int, int> x, Tuple<int, int> y)
			{
				int s = 0;
				bool minus = x.Item1 < y.Item1;
				int len = minus ? y.Item1 : x.Item1;
				Phonetic ph;
				for (int i = minus ? x.Item1 : y.Item1; i < len; i++)
					s += (minus ? -1 : 1) * (Phonetics.TryGetValue(i, out ph) ? ph.Text.Length : 1);
				return s + x.Item2 - y.Item2;
			}
		}

		abstract class LyricsItem
		{
			protected LyricsItem(int start, int end)
			{
				StartIndex = start;
				EndIndex = end;
			}

			public static void AnalyzeAll(IEnumerable<LyricsItem> items, StringBuilder sb, AnalyzerSink sink, bool createSyllable)
			{
				foreach (var item in items)
					item.Analyze(sink, sb, sb.Length, 0, createSyllable);
			}

			public abstract void Analyze(AnalyzerSink sink, StringBuilder sb, int index, int subIndex, bool createSyllable);

			public abstract IEnumerable<HighlightToken> GetTokens();

			public int StartIndex { get; private set; }

			public int EndIndex { get; private set; }
		}

		class SimpleItem : LyricsItem
		{
			public SimpleItem(string text, CharacterState state, int start, int end)
				: base(start, end)
			{
				State = state;
				Text = text;
			}

			public CharacterState State { get; private set; }

			public string Text { get; private set; }

			public override void Analyze(AnalyzerSink sink, StringBuilder sb, int index, int subIndex, bool createSyllable)
			{
				if (createSyllable)
				{
					if (State == CharacterState.StartGrouping)
						sink.SyllableStartIndex = new Tuple<int, int>(index, subIndex);
					else if (State == CharacterState.StopGrouping)
					{
						if (sink.SyllableStartIndex != null)
						{
							sink.Syllables.Add(new Syllable(sink.SyllableStartIndex.Item1, sink.SyllableStartIndex.Item2, sink.Subtract(new Tuple<int, int>(index, subIndex), sink.SyllableStartIndex)));
							sink.SyllableStartIndex = null;
						}
					}
					else if (State == CharacterState.Default)
					{
						if (sink.SyllableStartIndex == null && !string.IsNullOrWhiteSpace(Text))
							sink.Syllables.Add(new Syllable(index, subIndex, Text.Length));
					}
				}
				if (State == CharacterState.Default)
					sb.Append(Text);
			}

			public override IEnumerable<HighlightToken> GetTokens() { return Enumerable.Empty<HighlightToken>(); }

			public override string ToString() { return Text + ", " + State.ToString(); }
		}

		class DeletedItem : LyricsItem
		{
			public DeletedItem(LyricsItem[] items, int start, int end) : base(start, end) { Items = items; }

			public LyricsItem[] Items { get; private set; }

			public override void Analyze(AnalyzerSink sink, StringBuilder sb, int index, int subIndex, bool createSyllable) { AnalyzeAll(Items, sb, sink, false); }

			public override IEnumerable<HighlightToken> GetTokens() { yield return new HighlightToken(StartIndex, EndIndex - StartIndex, Color.Green, Color.Empty); }
		}

		class CompositeItem : LyricsItem
		{
			public CompositeItem(SimpleItem[] rawText, SimpleItem[] phonetic, int start, int end)
				: base(start, end)
			{
				_rawText = rawText;
				Phonetic = phonetic;
			}

			SimpleItem[] _rawText;

			public string Text { get { return string.Concat(_rawText.Where(x => x.State == CharacterState.Default).Select(x => x.Text)); } }

			public SimpleItem[] Phonetic { get; private set; }

			public override void Analyze(AnalyzerSink sink, StringBuilder sb, int index, int subIndex, bool createSyllable)
			{
				StringBuilder phoneticText = new StringBuilder();
				foreach (var phonetic in Phonetic)
					phonetic.Analyze(sink, phoneticText, sb.Length, phoneticText.Length, createSyllable);
				sink.Phonetics.Add(sb.Length, new Phonetic(Text.Length, phoneticText.ToString()));
				sb.Append(Text);
			}

			public override IEnumerable<HighlightToken> GetTokens()
			{
				yield return new HighlightToken(_rawText[0].StartIndex, _rawText[_rawText.Length - 1].EndIndex - _rawText[0].StartIndex, Color.Red, Color.Empty);
				yield return new HighlightToken(Phonetic[0].StartIndex, Phonetic[Phonetic.Length - 1].EndIndex - Phonetic[0].StartIndex, Color.Blue, Color.Empty);
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
