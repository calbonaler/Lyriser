using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Linq;
using System.Text;

namespace Lyriser
{
	public class Lyrics
	{
		public Lyrics() { Lines = new Collection<LyricsLine>(); }

		int _highlightLineIndex = 0;
		int _highlightSyllableId = 0;

		Font _mainFont = null;
		Font _phoneticFont = null;

		public Collection<LyricsLine> Lines { get; }

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

		public Rectangle ActualBounds => new Rectangle(Bounds.X, Bounds.Y, Bounds.Width, Bounds.Height - MainFont.Height - PhoneticFont.Height);

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

		public int MaxViewedLines => ActualBounds.Height / (MainFont.Height + PhoneticFont.Height + PhoneticOffset);

		public int ViewStartLineIndex { get; set; }

		public int VerticalScrollMaximum => Lines.Count - MaxViewedLines;
	}

	public class LyricsLine
	{
		internal LyricsLine(IEnumerable<LyricsItem> sections, SyllableIdProvider provider)
		{
			_sections = sections.ToArray();
			_syllableCount = provider.SyllableCount;
		}

		LyricsItem[] _sections;
		int _syllableCount;

		public void Draw(Graphics graphics, Font mainFont, Font phoneticFont, Brush brush, Brush highlightBrush, float x, float y, float phoneticOffsetY, int highlightSyllableId)
		{
			var rangeRects = LyricsItem.MeasureItems(graphics, _sections, mainFont);
			float[] xpos = new float[_sections.Length + 1];
			xpos[0] = x;
			for (int i = 0; i < _sections.Length; i++)
			{
				var advanceWidth = i + 1 < _sections.Length ? rangeRects[i + 1].X - rangeRects[i].X : rangeRects[i].Width;
				xpos[i + 1] = _sections[i].GetAdvanceWidth(graphics, mainFont, phoneticFont, advanceWidth) + xpos[i];
			}
			for (int i = 0; i < _sections.Length; i++)
				_sections[i].DrawHighlight(graphics, mainFont, phoneticFont, highlightBrush, xpos[i], y, xpos[i + 1] - xpos[i], phoneticOffsetY, highlightSyllableId);
			for (int i = 0; i < _sections.Length; i++)
				_sections[i].Draw(graphics, mainFont, phoneticFont, brush, xpos[i], y, rangeRects[i].Width, xpos[i + 1] - xpos[i], phoneticOffsetY);
		}

		public int SyllableCount => _syllableCount;

		public int SectionLength => _sections.Length;
	}

	public abstract class LyricsItem
	{
		protected LyricsItem(string text) { Text = text; }

		public string Text { get; }

		public static RectangleF[] MeasureItems(Graphics graphics, IEnumerable<LyricsItem> items, Font font)
		{
			List<CharacterRange> ranges = new List<CharacterRange>();
			StringBuilder sb = new StringBuilder();
			foreach (var item in items)
			{
				ranges.Add(new CharacterRange(sb.Length, item.Text.Length));
				sb.Append(item.Text);
			}
			return graphics.MeasureCharacterRanges(sb.ToString(), font, ranges);
		}

		public abstract float GetAdvanceWidth(Graphics graphics, Font mainFont, Font phoneticFont, float advanceWidth);

		public abstract void Draw(Graphics graphics, Font mainFont, Font phoneticFont, Brush brush, float x, float y, float textWidth, float width, float phoneticOffsetY);

		public abstract void DrawHighlight(Graphics graphics, Font mainFont, Font phoneticFont, Brush brush, float x, float y, float width, float phoneticOffsetY, int highlightSyllableId);
	}

	public class LyricsCharacterItem : LyricsItem
	{
		public LyricsCharacterItem(string text, int? syllableId) : base(text) { SyllableIdentifier = syllableId; }

		public int? SyllableIdentifier { get; }

		public override float GetAdvanceWidth(Graphics graphics, Font mainFont, Font phoneticFont, float advanceWidth) => advanceWidth;

		public override void Draw(Graphics graphics, Font mainFont, Font phoneticFont, Brush brush, float x, float y, float textWidth, float width, float phoneticOffsetY)
		{
			graphics.DrawString(Text, mainFont, brush, x, y + phoneticFont.Height + phoneticOffsetY, StringFormat.GenericTypographic);
		}

		public override void DrawHighlight(Graphics graphics, Font mainFont, Font phoneticFont, Brush brush, float x, float y, float width, float phoneticOffsetY, int highlightSyllableId)
		{
			if (SyllableIdentifier == highlightSyllableId)
				graphics.FillRectangle(brush, x, y + phoneticFont.Height + phoneticOffsetY, width, mainFont.Height);
		}
	}

	public class LyricsCompositeItem : LyricsItem
	{
		public LyricsCompositeItem(string text, IEnumerable<LyricsCharacterItem> phonetic) : base(text) { Phonetic = new ReadOnlyCollection<LyricsCharacterItem>(phonetic.ToArray()); }

		public ReadOnlyCollection<LyricsCharacterItem> Phonetic { get; }

		public override float GetAdvanceWidth(Graphics graphics, Font mainFont, Font phoneticFont, float advanceWidth)
		{
			var phoneticRects = MeasureItems(graphics, Phonetic, phoneticFont);
			return Math.Max(phoneticRects.Last().Right - phoneticRects.First().X, advanceWidth);
		}

		public override void Draw(Graphics graphics, Font mainFont, Font phoneticFont, Brush brush, float x, float y, float textWidth, float width, float phoneticOffsetY)
		{
			var phoneticRects = MeasureItems(graphics, Phonetic, phoneticFont);
			// ふりがなの描画
			for (int i = 0; i < Phonetic.Count; i++)
				graphics.DrawString(Phonetic[i].Text, phoneticFont, brush, x + i * width / Phonetic.Count + (width / Phonetic.Count - phoneticRects[i].Width) / 2, y, StringFormat.GenericTypographic);
			// ベーステキストの描画
			graphics.DrawString(Text, mainFont, brush, x + (width - textWidth) / 2, y + phoneticFont.Height + phoneticOffsetY, StringFormat.GenericTypographic);
		}

		public override void DrawHighlight(Graphics graphics, Font mainFont, Font phoneticFont, Brush brush, float x, float y, float width, float phoneticOffsetY, int highlightSyllableId)
		{
			for (int i = 0; i < Phonetic.Count; i++)
			{
				if (Phonetic[i].SyllableIdentifier == highlightSyllableId)
					graphics.FillRectangle(brush, x + i * width / Phonetic.Count, y, width / Phonetic.Count, phoneticFont.Height + phoneticOffsetY + mainFont.Height);
			}
		}
	}
}
