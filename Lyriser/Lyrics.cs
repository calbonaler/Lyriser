using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Lyriser
{
	public class Lyrics : IEnumerable<LyricsLine>
	{
		List<LyricsLine> lines = new List<LyricsLine>();

		int highlightedLineIndex = 0;
		int highlightedCharRangeIndex = 0;
		int highlightedCharSubRangeIndex = 0;

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
			for (int i = 0; i < MaxViewedLines && i < lines.Count; i++)
				lines[i + ViewStartLineIndex].Draw(graphics, MainFont, PhoneticFont, Brushes.Black, 5, i * (PhoneticFont.Height + PhoneticOffset + MainFont.Height),
					PhoneticOffset, highlightedLineIndex == i + ViewStartLineIndex ? highlightedCharRangeIndex : -1, highlightedCharSubRangeIndex);
			
			graphics.FillRectangle(Brushes.Gray, 0, ActualBounds.Bottom, Bounds.Width, Bounds.Height - ActualBounds.Height);
			int hi = highlightedLineIndex;
			do
			{
				if (++hi >= lines.Count)
					return;
			} while (lines[hi].Count <= 0);
			lines[hi].Draw(graphics, MainFont, PhoneticFont, Brushes.White, 5, ActualBounds.Bottom, PhoneticOffset, -1, 0);
		}

		public void ResetHighlightPosition()
		{
			if (lines.Count <= 0)
			{
				highlightedLineIndex = -1;
				return;
			}
			highlightedLineIndex = 0;
			while (lines[highlightedLineIndex].Count <= 0)
			{
				if (++highlightedLineIndex >= lines.Count)
				{
					highlightedLineIndex = -1;
					return;
				}
			}
			highlightedCharRangeIndex = highlightedCharSubRangeIndex = 0;
			if (highlightedLineIndex < ViewStartLineIndex || highlightedLineIndex > ViewStartLineIndex + MaxViewedLines - 1)
				ViewStartLineIndex = highlightedLineIndex;
		}

		public bool HighlightPrevious()
		{
			if (highlightedLineIndex < 0)
				return false;
			if (highlightedCharSubRangeIndex > 0)
				highlightedCharSubRangeIndex--;
			else if (highlightedCharRangeIndex > 0)
			{
				highlightedCharRangeIndex--;
				highlightedCharSubRangeIndex = lines[highlightedLineIndex][highlightedCharRangeIndex].PhoneticLength - 1;
			}
			else if (highlightedLineIndex > 0)
			{
				int hi = highlightedLineIndex;
				while (lines[--hi].Count <= 0)
				{
					if (hi <= 0)
						return false;
				}
				highlightedLineIndex = hi;
				highlightedCharRangeIndex = lines[highlightedLineIndex].Count - 1;
				highlightedCharSubRangeIndex = lines[highlightedLineIndex][highlightedCharRangeIndex].PhoneticLength - 1;
			}
			else
				return false;
			if (highlightedLineIndex < ViewStartLineIndex || highlightedLineIndex > ViewStartLineIndex + MaxViewedLines - 1)
				ViewStartLineIndex = highlightedLineIndex;
			return true;
		}

		public bool HighlightNext()
		{
			if (highlightedLineIndex < 0)
				return false;
			if (highlightedCharSubRangeIndex < lines[highlightedLineIndex][highlightedCharRangeIndex].PhoneticLength - 1)
				highlightedCharSubRangeIndex++;
			else if (highlightedCharRangeIndex < lines[highlightedLineIndex].Count - 1)
			{
				highlightedCharRangeIndex++;
				highlightedCharSubRangeIndex = 0;
			}
			else if (highlightedLineIndex < lines.Count - 1)
			{
				int hi = highlightedLineIndex;
				while (lines[++hi].Count <= 0)
				{
					if (hi >= lines.Count - 1)
						return false;
				}
				highlightedLineIndex = hi;
				highlightedCharRangeIndex = 0;
				highlightedCharSubRangeIndex = 0;
			}
			else
				return false;
			if (highlightedLineIndex < ViewStartLineIndex || highlightedLineIndex > ViewStartLineIndex + MaxViewedLines - 1)
			{
				ViewStartLineIndex = highlightedLineIndex - MaxViewedLines + 1;
				if (ViewStartLineIndex < 0)
					ViewStartLineIndex = 0;
			}
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

		public IEnumerator<LyricsLine> GetEnumerator() { return lines.GetEnumerator(); }

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() { return GetEnumerator(); }

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

	public class LyricsLine : IReadOnlyList<Syllable>
	{
		LyricsLine() { }

		List<Syllable> _syllables = new List<Syllable>();

		public static LyricsLine Parse(string sourceLine)
		{
			LyricsLine instance = new LyricsLine();
			sourceLine = Regex.Replace(sourceLine, @"\s+", " ");
			StringBuilder sb = new StringBuilder();
			foreach (Match item in Regex.Matches(sourceLine, @"{(?<content>[^""]+?)(""(?<phonetic>[^""]+?)"")?}|(?<content>[^""])""(?<phonetic>[^""]+?)""|\((?<noc>.*?)\)|(?<content>.)", RegexOptions.ExplicitCapture))
			{
				if (!item.Groups["noc"].Success)
				{
					StringBuilder subsb = new StringBuilder();
					var submatches = Regex.Matches(item.Groups["phonetic"].Value, @"{(?<content>.*?)}|\((?<noc>.*?)\)|(?<content>.)");
					int phonlen = 0;
					foreach (Match subitem in submatches)
					{
						if (!subitem.Groups["noc"].Success)
						{
							subsb.Append(subitem.Groups["content"].Value);
							phonlen++;
						}
						else
							subsb.Append(subitem.Groups["noc"].Value);
					}
					if (item.Groups["content"].Value != " ")
						instance._syllables.Add(new Syllable(sb.Length, item.Groups["content"].Length, subsb.ToString(), phonlen));
					sb.Append(item.Groups["content"].Value);
				}
				else
					sb.Append(item.Groups["noc"].Value);
			}
			instance.Line = sb.ToString();
			return instance;
		}

		public void Draw(Graphics graphics, Font mainFont, Font phoneticFont, Brush brush, int x, int y, int phoneticOffset, int highlightedCharRange, int highlightedCharSubRange)
		{
			if (mainFont == null)
				throw new ArgumentNullException("mainFont");
			if (phoneticFont == null)
				throw new ArgumentNullException("phoneticFont");
			using (StringFormat format = new StringFormat())
			{
				format.SetMeasurableCharacterRanges(_syllables.Select(s => s.Range).ToArray());
				var strLayoutRect = new RectangleF(x, y + phoneticFont.Height + phoneticOffset, (int)graphics.MeasureString(Line, mainFont).Width + 5, mainFont.Height);
				var rects = graphics.MeasureCharacterRanges(Line, mainFont, strLayoutRect, format).Select(r => r.GetBounds(graphics)).ToArray();
				for (int i = 0; i < _syllables.Count; i++)
				{
					if (highlightedCharRange == i)
					{
						int sum = string.IsNullOrEmpty(_syllables[i].Phonetic) ? 0 : phoneticFont.Height + phoneticOffset;
						graphics.FillRectangle(Brushes.Cyan, rects[i].X + rects[i].Width * highlightedCharSubRange / _syllables[i].PhoneticLength,
							rects[i].Y - sum, rects[i].Width / _syllables[i].PhoneticLength, rects[i].Height + sum);
					}
					var pw = (int)graphics.MeasureString(_syllables[i].Phonetic, phoneticFont).Width;
					graphics.DrawString(_syllables[i].Phonetic, phoneticFont, brush, rects[i].X + (rects[i].Width - pw) / 2, y);
				}
				graphics.DrawString(Line, mainFont, brush, strLayoutRect, format);
			}
		}

		public string Line { get; private set; }

		public IEnumerator<Syllable> GetEnumerator() { return _syllables.GetEnumerator(); }

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() { return GetEnumerator(); }

		public Syllable this[int index] { get { return _syllables[index]; } }

		public int Count { get { return _syllables.Count; } }
	}

	public struct Syllable : IEquatable<Syllable>
	{
		public Syllable(int first, int length, string phonetic) : this(first, length, phonetic, null) { }

		public Syllable(int first, int length, string phonetic, int? phoneticLength) : this()
		{
			Range = new CharacterRange(first, length);
			Phonetic = phonetic;
			this.phoneticLength = phoneticLength;
		}

		int? phoneticLength;

		public CharacterRange Range { get; set; }

		public int First
		{
			get { return Range.First; }
			set { Range = new CharacterRange(value, Range.Length); }
		}

		public int Length
		{
			get { return Range.Length; }
			set { Range = new CharacterRange(Range.First, value); }
		}

		public string Phonetic { get; set; }

		public int PhoneticLength
		{
			get
			{
				var len = phoneticLength ?? Phonetic.Length;
				if (len <= 0)
					len = 1;
				return len;
			}
			set { phoneticLength = value; }
		}

		public void ClearPhoneticLength() { phoneticLength = null; }

		public bool Equals(Syllable other) { return Range == other.Range && Phonetic == other.Phonetic && PhoneticLength == other.PhoneticLength; }

		public override bool Equals(object obj)
		{
			if (obj is Syllable)
				Equals((Syllable)obj);
			return false;
		}

		public override int GetHashCode() { return Range.GetHashCode() ^ Phonetic.GetHashCode() ^ PhoneticLength.GetHashCode(); }

		public static bool operator ==(Syllable left, Syllable right) { return left.Equals(right); }

		public static bool operator !=(Syllable left, Syllable right) { return !(left == right); }
	}
}
