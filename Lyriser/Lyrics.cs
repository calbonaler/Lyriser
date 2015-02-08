using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;

namespace Lyriser
{
	public class Lyrics
	{
		public Lyrics() { Lines = new List<LyricsLine>(); }

		int _highlightLineIndex = 0;
		int _highlightSyllableId = 0;

		Font _mainFont = null;
		Font _phoneticFont = null;

		public IList<LyricsLine> Lines { get; private set; }

		public void Draw(Graphics graphics)
		{
			if (graphics == null)
				throw new ArgumentNullException("graphics");
			graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
			for (int i = 0; i < MaxViewedLines && i + ViewStartLineIndex < Lines.Count; i++)
				Lines[i + ViewStartLineIndex].Draw(graphics, MainFont, PhoneticFont, Brushes.Black, Brushes.Cyan, 5, i * (PhoneticFont.Height + PhoneticOffset + MainFont.Height), PhoneticOffset, _highlightLineIndex == i + ViewStartLineIndex ? _highlightSyllableId : -1);
			graphics.FillRectangle(Brushes.Gray, 0, ActualBounds.Bottom, Bounds.Width, Bounds.Height - ActualBounds.Height);
			int hi = GetNextHighlightableLineIndex(_highlightLineIndex, true);
			if (hi >= 0)
				Lines[hi].Draw(graphics, MainFont, PhoneticFont, Brushes.White, null, 5, ActualBounds.Bottom, PhoneticOffset, -1);
		}

		public void ResetHighlightPosition()
		{
			ScrollInto(_highlightLineIndex = GetNextHighlightableLineIndex(-1, true));
			_highlightSyllableId = 0;
		}

		public bool HighlightNextLine(bool forward)
		{
			int nextHighlightable = GetNextHighlightableLineIndex(_highlightLineIndex, forward);
			if (nextHighlightable >= 0)
			{
				ScrollInto(_highlightLineIndex = nextHighlightable);
				_highlightSyllableId = Math.Min(_highlightSyllableId, Lines[nextHighlightable].SyllableCount - 1);
				return true;
			}
			return false;
		}

		int GetNextHighlightableLineIndex(int start, bool forward)
		{
			for (int i = start; ; )
			{
				i += forward ? 1 : -1;
				if (i < 0 || i >= Lines.Count)
					return -1;
				if (Lines[i].SyllableCount > 0)
					return i;
			}
		}

		public bool HighlightNext(bool forward)
		{
			if (_highlightLineIndex < 0 || _highlightLineIndex >= Lines.Count)
				return false;
			if (forward ? _highlightSyllableId < Lines[_highlightLineIndex].SyllableCount - 1 : _highlightSyllableId > 0)
			{
				_highlightSyllableId += forward ? 1 : -1;
				return true;
			}
			var nextHighlightable = GetNextHighlightableLineIndex(_highlightLineIndex, forward);
			if (nextHighlightable >= 0)
			{
				ScrollInto(_highlightLineIndex = nextHighlightable);
				_highlightSyllableId = forward ? 0 : Lines[nextHighlightable].SyllableCount - 1;
				return true;
			}
			return false;
		}

		public void ScrollInto(int line)
		{
			if (line >= 0 && line < Lines.Count)
			{
				if (line > ViewStartLineIndex + MaxViewedLines - 1)
					ViewStartLineIndex = Math.Max(line - MaxViewedLines + 1, 0);
				else if (line < ViewStartLineIndex)
					ViewStartLineIndex = line;
			}
		}

		public Rectangle ActualBounds { get { return new Rectangle(Bounds.X, Bounds.Y, Bounds.Width, Bounds.Height - MainFont.Height - PhoneticFont.Height); } }

		public Rectangle Bounds { get; set; }

		public Font MainFont
		{
			get { return _mainFont ?? SystemFonts.DefaultFont; }
			set { _mainFont = value; }
		}

		public Font PhoneticFont
		{
			get
			{
				if (_phoneticFont == null || _phoneticFont.FontFamily != MainFont.FontFamily)
					return _phoneticFont = new Font(MainFont.FontFamily, MainFont.Size / 2);
				return _phoneticFont;
			}
		}

		public int PhoneticOffset { get; set; }

		public int MaxViewedLines { get { return ActualBounds.Height / (MainFont.Height + PhoneticFont.Height + PhoneticOffset); } }

		public int ViewStartLineIndex { get; set; }

		public int VerticalScrollMaximum { get { return Lines.Count - MaxViewedLines; } }
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
