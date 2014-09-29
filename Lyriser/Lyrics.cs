using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace Lyriser
{
	public class Lyrics
	{
		List<LyricsLine> lines = new List<LyricsLine>();

		int highlightLine = 0;
		Syllable highlightSyllable = null;

		Font mainFont = null;
		Font phoneticFont = null;

		public IList<LyricsLine> Lines { get { return lines; } }

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
		public LyricsLine(string line, IEnumerable<Syllable> syllables, Dictionary<int, Phonetic> phonetics)
		{
			Line = line;
			_syllables = syllables.ToArray();
			_phonetics = phonetics;
		}

		Syllable[] _syllables;
		Dictionary<int, Phonetic> _phonetics;

		public Syllable GetNextSyllable(int index, int subIndex)
		{
			int itemIndex = Array.BinarySearch(_syllables, new Syllable(index, subIndex, 0), Comparer<Syllable>.Create(Syllable.CompareOrder));
			if (itemIndex >= 0)
				itemIndex++;
			else
				itemIndex = ~itemIndex;
			if (itemIndex < _syllables.Length)
				return _syllables[itemIndex];
			else
				return null;
		}

		public Syllable GetPreviousSyllable(int index, int subIndex)
		{
			int itemIndex = Array.BinarySearch(_syllables, new Syllable(index, subIndex, 0), Comparer<Syllable>.Create(Syllable.CompareOrder));
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

		public static int CompareOrder(Syllable left, Syllable right)
		{
			if (ReferenceEquals(right, null))
				return ReferenceEquals(left, null) ? 0 : 1;
			if (ReferenceEquals(left, null))
				return -1;
			var comp = left.Index.CompareTo(right.Index);
			if (comp != 0)
				return comp;
			else
				return left.SubIndex.CompareTo(right.SubIndex);
		}
	}

	public class Phonetic
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
