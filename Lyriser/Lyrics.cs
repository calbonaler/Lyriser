using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;

namespace Lyriser
{
	public class Lyrics
	{
		List<LyricsLine> lines = new List<LyricsLine>();

		int highlightLine = 0;
		int highlightSyllableId = -1;

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
				lines[i + ViewStartLineIndex].Draw(graphics, MainFont, PhoneticFont, Brushes.Black, Brushes.Cyan, 5, i * (PhoneticFont.Height + PhoneticOffset + MainFont.Height), PhoneticOffset, highlightLine == i + ViewStartLineIndex ? highlightSyllableId : -1);
			graphics.FillRectangle(Brushes.Gray, 0, ActualBounds.Bottom, Bounds.Width, Bounds.Height - ActualBounds.Height);
			int hi = highlightLine;
			do
			{
				if (++hi >= lines.Count)
					return;
			} while (lines[hi].SectionLength <= 0);
			lines[hi].Draw(graphics, MainFont, PhoneticFont, Brushes.White, null, 5, ActualBounds.Bottom, PhoneticOffset, -1);
		}

		public void ResetHighlightPosition()
		{
			if (lines.Count <= 0)
			{
				highlightLine = -1;
				return;
			}
			highlightLine = 0;
			highlightSyllableId = 0;
			while (lines[highlightLine].SyllableCount <= 0)
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
			var syllable = highlightSyllableId;
			while (--syllable < 0)
			{
				if (--hi < 0)
					return false;
				syllable = lines[hi].SyllableCount;
			}
			highlightLine = hi;
			highlightSyllableId = syllable;
			ScrollInto(highlightLine);
			return true;
		}

		public bool HighlightNext()
		{
			if (highlightLine < 0)
				return false;
			int hi = highlightLine;
			var syllable = highlightSyllableId;
			while (++syllable >= lines[hi].SyllableCount)
			{
				if (++hi >= lines.Count)
					return false;
				syllable = -1;
			}
			highlightLine = hi;
			highlightSyllableId = syllable;
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
		internal LyricsLine(IEnumerable<LyricsSection> sections, SyllableIdProvider provider)
		{
			_sections = sections.ToArray();
			_syllableCount = provider.SyllableCount;
		}

		LyricsSection[] _sections;
		int _syllableCount;

		public void Draw(Graphics graphics, Font mainFont, Font phoneticFont, Brush brush, Brush highlightBrush, int x, int y, int phoneticOffset, int highlightSyllableId)
		{
			CharacterRange[] ranges = new CharacterRange[_sections.Length];
			StringBuilder sb = new StringBuilder();
			for (int i = 0; i < _sections.Length; i++)
			{
				ranges[i] = new CharacterRange(sb.Length, _sections[i].Text.Length);
				sb.Append(_sections[i].Text);
			}
			int offset;
			var rangeWidths = graphics.MeasureCharacterRangeWidths(sb.ToString(), mainFont, ranges, out offset);
			for (int i = 0; i < _sections.Length; i++)
				x += _sections[i].Draw(graphics, mainFont, phoneticFont, brush, highlightBrush, x, y, rangeWidths[i], phoneticOffset, offset, highlightSyllableId);
		}

		public int SyllableCount { get { return _syllableCount; } }

		public int SectionLength { get { return _sections.Length; } }
	}

	public abstract class LyricsSection
	{
		protected LyricsSection(string text) { Text = text; }

		public string Text { get; private set; }

		public abstract int Draw(Graphics graphics, Font mainFont, Font phoneticFont, Brush brush, Brush highlightBrush, int x, int y, int textWidth, int phoneticOffset, int textOffset, int highlightSyllableId);
	}

	public class LyricsCharacterSection : LyricsSection
	{
		public LyricsCharacterSection(string text, int? syllableId) : base(text) { SyllableIdentifier = syllableId; }

		public int? SyllableIdentifier { get; private set; }

		public override int Draw(Graphics graphics, Font mainFont, Font phoneticFont, Brush brush, Brush highlightBrush, int x, int y, int textWidth, int phoneticOffset, int textOffset, int highlightSyllableId)
		{
			// ハイライトの描画
			if (SyllableIdentifier == highlightSyllableId)
				graphics.FillRectangle(highlightBrush, x + textOffset, y + phoneticFont.Height + phoneticOffset, textWidth, mainFont.Height);
			// ベーステキストの描画
			graphics.DrawString(Text, mainFont, brush, x, y + phoneticFont.Height + phoneticOffset);
			return textWidth;
		}
	}

	public class LyricsCompositeSection : LyricsSection
	{
		public LyricsCompositeSection(string text, IEnumerable<LyricsCharacterSection> phonetic) : base(text) { Phonetic = phonetic.ToArray(); }

		public IReadOnlyList<LyricsCharacterSection> Phonetic { get; private set; }

		public override int Draw(Graphics graphics, Font mainFont, Font phoneticFont, Brush brush, Brush highlightBrush, int x, int y, int textWidth, int phoneticOffset, int textOffset, int highlightSyllableId)
		{
			var phoneticText = string.Concat(Phonetic.Select(p => p.Text));
			var phoneticWidth = graphics.MeasureStringWidth(phoneticText, phoneticFont);
			var drawingWidth = Math.Max(phoneticWidth, textWidth);
			// ハイライトの描画
			for (int j = 0; j < Phonetic.Count; j++)
			{
				if (Phonetic[j].SyllableIdentifier == highlightSyllableId)
					graphics.FillRectangle(highlightBrush, x + textOffset + MathUtils.CeilingDivide(j * drawingWidth, Phonetic.Count), y, MathUtils.CeilingDivide(drawingWidth, Phonetic.Count), phoneticFont.Height + phoneticOffset + mainFont.Height);
			}
			// ふりがなの描画
			graphics.DrawString(phoneticText, phoneticFont, brush, x + textOffset * phoneticFont.Height / mainFont.Height + Math.Max(textWidth - phoneticWidth, 0) / 2, y);
			// ベーステキストの描画
			graphics.DrawString(Text, mainFont, brush, x + Math.Max(phoneticWidth - textWidth, 0) / 2, y + phoneticFont.Height + phoneticOffset);
			return drawingWidth;
		}
	}
}
