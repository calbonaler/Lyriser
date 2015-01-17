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
			graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
			if (MaxViewedLines >= lines.Count)
				ViewStartLineIndex = 0;
			else if (ViewStartLineIndex > VerticalScrollMaximum)
				ViewStartLineIndex = VerticalScrollMaximum;
			for (int i = 0; i < MaxViewedLines && i + ViewStartLineIndex < lines.Count; i++)
				lines[i + ViewStartLineIndex].Draw(graphics, MainFont, PhoneticFont, Brushes.Black, Brushes.Cyan, 5, i * (PhoneticFont.Height + PhoneticOffset + MainFont.Height), PhoneticOffset, highlightLine == i + ViewStartLineIndex ? highlightSyllable : null);
			graphics.FillRectangle(Brushes.Gray, 0, ActualBounds.Bottom, Bounds.Width, Bounds.Height - ActualBounds.Height);
			int hi = highlightLine;
			do
			{
				if (++hi >= lines.Count)
					return;
			} while (lines[hi].Line.Length <= 0);
			lines[hi].Draw(graphics, MainFont, PhoneticFont, Brushes.White, null, 5, ActualBounds.Bottom, PhoneticOffset, null);
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
				syllable = lines[hi].GetPreviousSyllable(syllable != null ? syllable.Start.Index : int.MaxValue, syllable != null ? syllable.Start.SubIndex : 0);
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
				syllable = lines[hi].GetNextSyllable(syllable != null ? syllable.Start.Index : -1, syllable != null ? syllable.Start.SubIndex : 0);
			} while (syllable == null && ++hi < lines.Count);
			if (syllable == null)
				return false;
			highlightLine = hi;
			highlightSyllable = syllable;
			ScrollInto(highlightLine);
			return true;
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
			int itemIndex = Array.BinarySearch(_syllables, new Syllable(new CharacterPointer(index, subIndex), new CharacterPointer(0, 0)), Comparer<Syllable>.Create(Syllable.CompareOrder));
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
			int itemIndex = Array.BinarySearch(_syllables, new Syllable(new CharacterPointer(index, subIndex), new CharacterPointer(0, 0)), Comparer<Syllable>.Create(Syllable.CompareOrder));
			if (itemIndex >= 0)
				itemIndex--;
			else
				itemIndex = ~itemIndex - 1;
			if (itemIndex >= 0)
				return _syllables[itemIndex];
			else
				return null;
		}

		public void Draw(Graphics graphics, Font mainFont, Font phoneticFont, Brush brush, Brush highlightBrush, int x, int y, int phoneticOffset, Syllable highlightSyllable)
		{
			if (mainFont == null)
				throw new ArgumentNullException("mainFont");
			if (phoneticFont == null)
				throw new ArgumentNullException("phoneticFont");
			int offset;
			var charWidths = graphics.MeasureCharacterWidths(Line, mainFont, Enumerable.Range(0, Line.Length).Select(i => new CharacterRange(i, 1)), out offset);
			for (int i = 0; i < Line.Length; )
			{
				Phonetic phonetic;
				if (_phonetics.TryGetValue(i, out phonetic))
				{
					var phoneticWidth = graphics.MeasureStringWidth(phonetic.Text, phoneticFont);
					var baseTextWidth = charWidths.Skip(i).Take(phonetic.Length).Sum();
					var drawingWidth = Math.Max(phoneticWidth, baseTextWidth);
					// ハイライトの描画
					if (highlightSyllable != null && i >= highlightSyllable.Start.Index && i <= highlightSyllable.End.Index)
					{
						for (int j = i > highlightSyllable.Start.Index ? 0 : highlightSyllable.Start.SubIndex; j < phonetic.Text.Length && (i < highlightSyllable.End.Index || j < highlightSyllable.End.SubIndex); j++)
							graphics.FillRectangle(highlightBrush, x + offset + MathUtils.CeilingDivide(j * drawingWidth, phonetic.Text.Length), y, MathUtils.CeilingDivide(drawingWidth, phonetic.Text.Length), phoneticFont.Height + phoneticOffset + mainFont.Height);
					}
					// ふりがなの描画
					graphics.DrawString(phonetic.Text, phoneticFont, brush, x + offset * phoneticFont.Height / mainFont.Height + Math.Max(baseTextWidth - phoneticWidth, 0) / 2, y);
					// ベーステキストの描画
					graphics.DrawString(Line.Substring(i, phonetic.Length), mainFont, brush, x + Math.Max(phoneticWidth - baseTextWidth, 0) / 2, y + phoneticFont.Height + phoneticOffset);
					x += drawingWidth;
					i += Math.Max(1, phonetic.Length);
				}
				else
				{
					// ハイライトの描画
					if (highlightSyllable != null && i >= highlightSyllable.Start.Index && i < highlightSyllable.End.Index)
						graphics.FillRectangle(highlightBrush, x + offset, y + phoneticFont.Height + phoneticOffset, charWidths[i], mainFont.Height);
					// ベーステキストの描画
					graphics.DrawString(Line[i].ToString(), mainFont, brush, x, y + phoneticFont.Height + phoneticOffset);
					x += charWidths[i];
					i++;
				}
			}
		}

		public string Line { get; private set; }
	}

	public class CharacterPointer
	{
		public CharacterPointer(int index, int subIndex)
		{
			Index = index;
			SubIndex = subIndex;
		}

		public int Index { get; private set; }

		public int SubIndex { get; private set; }

		public static int Compare(CharacterPointer left, CharacterPointer right)
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

	public class Syllable
	{
		public static int CompareOrder(Syllable left, Syllable right) { return CharacterPointer.Compare(left.Start, right.Start); }

		public Syllable(CharacterPointer start, CharacterPointer end)
		{
			Start = start;
			End = end;
		}

		public CharacterPointer Start { get; private set; }

		public CharacterPointer End { get; private set; }
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
