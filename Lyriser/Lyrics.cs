using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Controls;

namespace Lyriser
{
	public class Lyrics
	{
		List<LyricsLine> lines = new List<LyricsLine>();

		int highlightLine = 0;
		Syllable highlightSyllable = null;

		Font mainFont = null;
		Font phoneticFont = null;

		public void Clear() { lines.Clear(); }

		public void Parse(string source)
		{
			if (source == null)
				throw new ArgumentNullException("source");
			lines.Clear();
			foreach (var item in source.Split(new[] { "\n", "\r", "\r\n" }, StringSplitOptions.None))
				lines.Add(LyricsLine.Parse(item));
			ResetHighlightPosition();
		}

		public void Draw(Graphics graphics)
		{
			if (graphics == null)
				throw new ArgumentNullException("graphics");
			if (MaxViewedLines >= lines.Count)
				ViewStartLineIndex = 0;
			else if (ViewStartLineIndex > VerticalScrollMaximum)
				ViewStartLineIndex = VerticalScrollMaximum;
			for (int i = 0; i < MaxViewedLines && i + ViewStartLineIndex < lines.Count; i++)
			{
				lines[i + ViewStartLineIndex].Draw(graphics, MainFont, PhoneticFont, Brushes.Black, 5, i * (PhoneticFont.Height + PhoneticOffset + MainFont.Height),
					PhoneticOffset, highlightLine == i + ViewStartLineIndex ? highlightSyllable : null);
			}
			graphics.FillRectangle(Brushes.Gray, 0, ActualBounds.Bottom, Bounds.Width, Bounds.Height - ActualBounds.Height);
			int hi = highlightLine;
			do
			{
				if (++hi >= lines.Count)
					return;
			} while (lines[hi].Line.Length <= 0);
			lines[hi].Draw(graphics, MainFont, PhoneticFont, Brushes.White, 5, ActualBounds.Bottom, PhoneticOffset, null);
		}

		public void ResetHighlightPosition()
		{
			if (lines.Count <= 0)
			{
				highlightLine = -1;
				return;
			}
			highlightLine = 0;
			while ((highlightSyllable = lines[highlightLine].GetNextSyllable(-1, 0)) == null)
			{
				if (++highlightLine >= lines.Count)
				{
					highlightLine = -1;
					return;
				}
			}
			ScrollInto(highlightLine);
		}

		public bool HighlightPrevious()
		{
			if (highlightLine < 0)
				return false;
			int hi = highlightLine;
			Syllable syllable = highlightSyllable;
			do
			{
				syllable = lines[hi].GetPreviousSyllable(syllable != null ? syllable.Index : int.MaxValue, syllable != null ? syllable.SubIndex : 0);
			} while (syllable == null && --hi >= 0);
			if (syllable == null)
				return false;
			highlightLine = hi;
			highlightSyllable = syllable;
			ScrollInto(highlightLine);
			return true;
		}

		public bool HighlightNext()
		{
			if (highlightLine < 0)
				return false;
			int hi = highlightLine;
			Syllable syllable = highlightSyllable;
			do
			{
				syllable = lines[hi].GetNextSyllable(syllable != null ? syllable.Index : -1, syllable != null ? syllable.SubIndex : 0);
			} while (syllable == null && ++hi < lines.Count);
			if (syllable == null)
				return false;
			highlightLine = hi;
			highlightSyllable = syllable;
			ScrollInto(highlightLine);
			return true;
		}

		public void ScrollUp()
		{
			if (--ViewStartLineIndex < 0)
				ViewStartLineIndex = 0;
		}

		public void ScrollDown()
		{
			if (++ViewStartLineIndex > VerticalScrollMaximum)
				ViewStartLineIndex = VerticalScrollMaximum;
		}

		public void ScrollInto(int line)
		{
			if (line > ViewStartLineIndex + MaxViewedLines - 1)
				ViewStartLineIndex = Math.Max(line - MaxViewedLines + 1, 0);
			else if (line < ViewStartLineIndex)
				ViewStartLineIndex = line;
		}

		public Rectangle ActualBounds { get { return new Rectangle(Bounds.X, Bounds.Y, Bounds.Width, Bounds.Height - MainFont.Height - PhoneticFont.Height); } }

		public Rectangle Bounds { get; set; }

		public Font MainFont
		{
			get { return mainFont ?? SystemFonts.DefaultFont; }
			set { mainFont = value; }
		}

		public Font PhoneticFont
		{
			get
			{
				if (phoneticFont == null || phoneticFont.FontFamily != MainFont.FontFamily)
					return phoneticFont = new Font(MainFont.FontFamily, MainFont.Size / 2);
				return phoneticFont;
			}
		}

		public int PhoneticOffset { get; set; }

		public int MaxViewedLines { get { return ActualBounds.Height / (MainFont.Height + PhoneticFont.Height + PhoneticOffset); } }

		public int ViewStartLineIndex { get; set; }

		public int VerticalScrollMaximum { get { return lines.Count - MaxViewedLines; } }
	}

	public class LyricsLine
	{
		LyricsLine(IEnumerable<LyricsItem> items)
		{
			AnalyzerSink sink = new AnalyzerSink();
			StringBuilder sb = new StringBuilder();
			LyricsItem.AnalyzeAll(items, sb, sink, true);
			Line = sb.ToString();
			_syllables = sink.Syllables;
			_phonetics = sink.Phonetics;
		}

		List<Syllable> _syllables;
		Dictionary<int, Phonetic> _phonetics;

		public static LyricsLine Parse(string line) { return new Parser(line).Parse(); }

		public Syllable GetNextSyllable(int index, int subIndex)
		{
			int itemIndex = _syllables.BinarySearch(new Syllable(index, subIndex, 0), Comparer<Syllable>.Create(Syllable.CompareOrder));
			if (itemIndex >= 0)
				itemIndex++;
			else
				itemIndex = ~itemIndex;
			if (itemIndex < _syllables.Count)
				return _syllables[itemIndex];
			else
				return null;
		}

		public Syllable GetPreviousSyllable(int index, int subIndex)
		{
			int itemIndex = _syllables.BinarySearch(new Syllable(index, subIndex, 0), Comparer<Syllable>.Create(Syllable.CompareOrder));
			if (itemIndex >= 0)
				itemIndex--;
			else
				itemIndex = ~itemIndex - 1;
			if (itemIndex >= 0)
				return _syllables[itemIndex];
			else
				return null;
		}

		IEnumerable<Tuple<int, int?>> GetStringIndexes(int index, int subIndex, int length)
		{
			for (int i = index; i < Line.Length; i++)
			{
				Phonetic ph;
				if (_phonetics.TryGetValue(i, out ph))
				{
					for (int j = i > index ? 0 : subIndex; j < ph.Text.Length; j++)
					{
						yield return new Tuple<int, int?>(i, j);
						length--;
						if (length <= 0)
							yield break;
					}
				}
				else
				{
					yield return new Tuple<int, int?>(i, null);
					length--;
					if (length <= 0)
						yield break;
				}
			}
		}

		public string PhoneticSubstring(int index, int subIndex, int length)
		{
			StringBuilder sb = new StringBuilder();
			foreach (var i in GetStringIndexes(index, subIndex, length))
				sb.Append(i.Item2 == null ? Line[i.Item1] : _phonetics[i.Item1].Text[(int)i.Item2]);
			return sb.ToString();
		}
		
		public void Draw(Graphics graphics, Font mainFont, Font phoneticFont, Brush brush, int x, int y, int phoneticOffset, Syllable highlightSyllable)
		{
			if (mainFont == null)
				throw new ArgumentNullException("mainFont");
			if (phoneticFont == null)
				throw new ArgumentNullException("phoneticFont");
			using (StringFormat format = new StringFormat())
			{
				CharacterRange[] charRanges;
				RectangleF[] rects;
				var strLayoutRect = new RectangleF(x, y + phoneticFont.Height + phoneticOffset, (int)graphics.MeasureString(Line, mainFont).Width + 5, mainFont.Height);
				// 音節の強調表示
				if (highlightSyllable != null)
				{
					var indexes = GetStringIndexes(highlightSyllable.Index, highlightSyllable.SubIndex, highlightSyllable.Length).ToArray();
					charRanges = indexes.Select(r => new CharacterRange(r.Item1, r.Item2 != null ? _phonetics[r.Item1].Length : 1)).Distinct().ToArray();
					format.SetMeasurableCharacterRanges(charRanges);
					rects = graphics.MeasureCharacterRanges(Line, mainFont, strLayoutRect, format).Select(r => r.GetBounds(graphics)).ToArray();
					foreach (var index in indexes)
					{
						var rect = rects[Array.FindIndex(charRanges, r => r.First == index.Item1)];
						if (index.Item2 != null)
						{
							int sum = phoneticFont.Height + phoneticOffset;
							graphics.FillRectangle(Brushes.Cyan, rect.X + rect.Width * (int)index.Item2 / _phonetics[index.Item1].Text.Length,
								rect.Y - sum, rect.Width / _phonetics[index.Item1].Text.Length, rect.Height + sum);
						}
						else
							graphics.FillRectangle(Brushes.Cyan, rect);
					}
				}
				// ふりがなの表示
				charRanges = _phonetics.Select(r => new CharacterRange(r.Key, r.Value.Length)).ToArray();
				format.SetMeasurableCharacterRanges(charRanges);
				rects = graphics.MeasureCharacterRanges(Line, mainFont, strLayoutRect, format).Select(r => r.GetBounds(graphics)).ToArray();
				for (int i = 0; i < charRanges.Length; i++)
				{
					var pw = (int)graphics.MeasureString(_phonetics[charRanges[i].First].Text, phoneticFont).Width;
					graphics.DrawString(_phonetics[charRanges[i].First].Text, phoneticFont, brush, rects[i].X + (rects[i].Width - pw) / 2, y);
				}
				// 基本テキストの表示
				graphics.DrawString(Line, mainFont, brush, strLayoutRect, format);
			}
		}

		public string Line { get; private set; }

		public static IHighlightTokenizer CreateHighlightTokenizer() { return new LyricsHighlightTokenizer(); }

		class LyricsHighlightTokenizer : IHighlightTokenizer
		{
			public IEnumerable<HighlightToken> GetTokens(string text)
			{
				List<HighlightToken> tokens = new List<HighlightToken>();
				foreach (var item in text.Split(new[] { "\n", "\r", "\r\n" }, StringSplitOptions.None))
				{
					try
					{
						foreach (var li in new Parser(item).GetLyricsItems())
						{
							foreach (var token in li.GetTokens())
								tokens.Add(token);
						}
					}
					catch
					{

					}
				}
				return tokens;
			}
		}

		class Parser
		{
			public Parser(string line) { _line = line; }

			string _line;
			int _index;

			bool Accept(string chars)
			{
				if (_line.Length - _index < chars.Length)
					return false;
				for (int i = _index, j = 0; i < _line.Length && j < chars.Length; i++, j++)
				{
					if (_line[i] != chars[j])
						return false;
				}
				_index += chars.Length;
				return true;
			}

			public IList<LyricsItem> GetLyricsItems()
			{
				List<LyricsItem> items = new List<LyricsItem>();
				while (_index < _line.Length)
					items.Add(ParseLyricsItem());
				return items;
			}

			public LyricsLine Parse() { return new LyricsLine(GetLyricsItems()); }

			LyricsItem ParseLyricsItem()
			{
				int start = _index;
				if (Accept("("))
				{
					List<LyricsItem> items = new List<LyricsItem>();
					do
					{
						items.Add(ParseLyricsItem());
					} while (!Accept(")"));
					return new DeletedItem(items.ToArray(), start, _index);
				}
				if (Accept("["))
				{
					List<SimpleItem> mainItems = new List<SimpleItem>();
					do
					{
						mainItems.Add(ParseSimpleItem());
					} while (!Accept("\""));
					List<SimpleItem> items = new List<SimpleItem>();
					do
					{
						items.Add(ParseSimpleItem());
					} while (!Accept("\""));
					if (!Accept("]"))
						throw new ArgumentException();
					return new CompositeItem(mainItems.ToArray(), items.ToArray(), start, _index);
				}
				else
				{
					var item = ParseSimpleItem();
					if (Accept("\""))
					{
						List<SimpleItem> items = new List<SimpleItem>();
						do
						{
							items.Add(ParseSimpleItem());
						} while (!Accept("\""));
						return new CompositeItem(new[] { item }, items.ToArray(), start, _index);
					}
					else
						return item;
				}
			}

			SimpleItem ParseSimpleItem()
			{
				int start = _index;
				bool escaping = false;
				if (Accept("`"))
					escaping = true;
				StringBuilder sb = new StringBuilder();
				sb.Append(_line[_index++]);
				if (char.IsHighSurrogate(sb[sb.Length - 1]) && _index < _line.Length && char.IsLowSurrogate(_line[_index]))
					sb.Append(_line[_index++]);
				var text = sb.ToString();
				CharacterState state = CharacterState.Default;
				if (!escaping)
				{
					if (text == "{")
						state = CharacterState.StartGrouping;
					else if (text == "}")
						state = CharacterState.StopGrouping;
				}
				return new SimpleItem(text, state, start, _index);
			}
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
			public SimpleItem(string text, CharacterState state, int start, int end) : base(start, end)
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
			public CompositeItem(SimpleItem[] rawText, SimpleItem[] phonetic, int start, int end) : base(start, end)
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

		class Phonetic
		{
			public Phonetic(int length, string text)
			{
				Length = length;
				Text = text;
			}

			public int Length { get; private set; }

			public string Text { get; private set; }
		}
	}

	public class Syllable
	{
		public Syllable(int index, int subIndex, int length)
		{
			Index = index;
			SubIndex = subIndex;
			Length = length;
		}

		public int Index { get; private set; }

		public int SubIndex { get; private set; }

		public int Length { get; private set; }

		public static int CompareOrder(Syllable x, Syllable y)
		{
			if (ReferenceEquals(y, null))
				return ReferenceEquals(x, null) ? 0 : 1;
			if (ReferenceEquals(x, null))
				return -1;
			var comp = x.Index.CompareTo(y.Index);
			if (comp != 0)
				return comp;
			else
				return x.SubIndex.CompareTo(y.SubIndex);
		}
	}
}
