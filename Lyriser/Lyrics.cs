using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Drawing;
using System.Linq;
using System.Text;

namespace Lyriser
{
	public class Lyrics
	{
		public Lyrics()
		{
			Lines = new ObservableCollection<LyricsLine>();
			Lines.CollectionChanged += Lines_CollectionChanged;
		}

		int _highlightLineIndex = 0;
		int _highlightSyllableId = 0;
		Font _mainFont = null;
		Font _phoneticFont = null;
		bool _shouldMeasure = true;
		const int LyricsXOffset = 5;

		public ObservableCollection<LyricsLine> Lines { get; }

		void Lines_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
		{
			if (e.Action == NotifyCollectionChangedAction.Add || e.Action == NotifyCollectionChangedAction.Replace || e.Action == NotifyCollectionChangedAction.Reset)
				_shouldMeasure = true;
		}

		int GetNextHighlightableLineIndex(int start, bool forward)
		{
			for (int i = start; ;)
			{
				i += forward ? 1 : -1;
				if (i < 0 || i >= Lines.Count)
					return -1;
				if (Lines[i].SyllableCount > 0)
					return i;
			}
		}

		public (int lineIndex, int syllableIndex) HitTestSyllable(PointF point)
		{
			Func<int, double> ycoord = l => (l - ViewStartLineIndex + 0.5) * (PhoneticFont.Height + PhoneticOffset + MainFont.Height);
			int line = -1;
			for (int i = 0; i < ViewStartLineIndex + MaxViewedLines && i < Lines.Count; i++)
			{
				if (Lines[i].SyllableCount <= 0)
					continue;
				if (line < 0 || Math.Abs(ycoord(i) - point.Y) < Math.Abs(ycoord(line) - point.Y))
					line = i;
			}
			if (line < 0)
				return (line, 0);
			return (line, (int)Lines[line].GetNearestSyllableItem(point.X - LyricsXOffset).SyllableIdentifier);
		}

		public void Draw(Graphics graphics)
		{
			if (graphics == null)
				throw new ArgumentNullException("graphics");
			graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
			if (_shouldMeasure)
			{
				foreach (var line in Lines)
					line.Measure(graphics, MainFont, PhoneticFont);
				_shouldMeasure = false;
			}
			for (int i = 0; i < MaxViewedLines && i + ViewStartLineIndex < Lines.Count; i++)
			{
				if (_highlightLineIndex == i + ViewStartLineIndex)
					Lines[i + ViewStartLineIndex].DrawHighlight(graphics, MainFont, PhoneticFont, Brushes.Cyan, LyricsXOffset, i * (PhoneticFont.Height + PhoneticOffset + MainFont.Height), PhoneticOffset, _highlightSyllableId);
				Lines[i + ViewStartLineIndex].Draw(graphics, MainFont, PhoneticFont, Brushes.Black, LyricsXOffset, i * (PhoneticFont.Height + PhoneticOffset + MainFont.Height), PhoneticOffset);
			}
			graphics.FillRectangle(Brushes.Gray, 0, ActualBounds.Bottom, Bounds.Width, Bounds.Height - ActualBounds.Height);
			int hi = GetNextHighlightableLineIndex(_highlightLineIndex, true);
			if (hi >= 0)
				Lines[hi].Draw(graphics, MainFont, PhoneticFont, Brushes.White, LyricsXOffset, ActualBounds.Bottom, PhoneticOffset);
		}

		public void ResetHighlightPosition() { Highlight(GetNextHighlightableLineIndex(-1, true), _ => 0); }

		public bool Highlight(int line, Func<int, int> syllableIdCreator)
		{
			if (line < 0 || line >= Lines.Count)
				return false;
			var syllableId = syllableIdCreator(line);
			if (syllableId < 0 || syllableId >= Lines[line].SyllableCount)
				return false;
			_highlightLineIndex = line;
			_highlightSyllableId = syllableId;
			ScrollInto(_highlightLineIndex);
			return true;
		}

		public bool HighlightNextLine(bool forward) => Highlight(GetNextHighlightableLineIndex(_highlightLineIndex, forward), next =>
		{
			var syllables = Lines[_highlightLineIndex].GetItemsForSyllableId(_highlightSyllableId);
			var center = (syllables[0].Left + syllables[syllables.Length - 1].Left + syllables[syllables.Length - 1].AdvanceWidth) / 2;
			return (int)Lines[next].GetNearestSyllableItem(center).SyllableIdentifier;
		});

		public bool HighlightNext(bool forward)
		{
			if (_highlightLineIndex < 0 || _highlightLineIndex >= Lines.Count)
				return false;
			if (forward ? _highlightSyllableId < Lines[_highlightLineIndex].SyllableCount - 1 : _highlightSyllableId > 0)
			{
				_highlightSyllableId += forward ? 1 : -1;
				return true;
			}
			return Highlight(GetNextHighlightableLineIndex(_highlightLineIndex, forward), next => forward ? 0 : Lines[next].SyllableCount - 1);
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
			set
			{
				if (_mainFont != value)
				{
					_mainFont = value;
					_shouldMeasure = true;
				}
			}
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

		public int VerticalScrollMaximum => Math.Max(0, Lines.Count - MaxViewedLines);
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

		IEnumerable<LyricsCharacterItem> CharacterItems()
		{
			foreach (var item in _sections)
			{
				if (item is LyricsCharacterItem chItem)
					yield return chItem;
				else
				{
					foreach (var subItem in ((LyricsCompositeItem)item).Phonetic)
						yield return subItem;
				}
			}
		}

		public LyricsCharacterItem[] GetItemsForSyllableId(int syllableId) => CharacterItems().Where(x => x.SyllableIdentifier == syllableId).ToArray();

		public LyricsCharacterItem GetNearestSyllableItem(float x) =>
			CharacterItems().Where(i => i.SyllableIdentifier != null).Aggregate((LyricsCharacterItem)null, (a, b) => a == null || Math.Abs(b.Left + b.AdvanceWidth / 2 - x) < Math.Abs(a.Left + a.AdvanceWidth / 2 - x) ? b : a);

		public void Measure(Graphics graphics, Font mainFont, Font phoneticFont)
		{
			LyricsItem.MeasureItems(graphics, _sections, mainFont);
			for (int i = 0; i < _sections.Length; i++)
			{
				_sections[i].Left = i <= 0 ? 0 : _sections[i - 1].AdvanceWidth + _sections[i - 1].Left;
				_sections[i].AdvanceWidth = _sections[i].TextWidth;
				_sections[i].Measure(graphics, mainFont, phoneticFont);
			}
		}

		public void Draw(Graphics graphics, Font mainFont, Font phoneticFont, Brush brush, float left, float top, float phoneticOffsetY)
		{
			top += phoneticFont.Height + phoneticOffsetY + mainFont.Height;
			for (int i = 0; i < _sections.Length; i++)
				_sections[i].Draw(graphics, mainFont, phoneticFont, brush, left, top, phoneticOffsetY);
		}

		public void DrawHighlight(Graphics graphics, Font mainFont, Font phoneticFont, Brush brush, float left, float top, float phoneticOffsetY, int highlightSyllableId)
		{
			top += phoneticFont.Height + phoneticOffsetY + mainFont.Height;
			for (int i = 0; i < _sections.Length; i++)
				_sections[i].DrawHighlight(graphics, mainFont, phoneticFont, brush, left, top, phoneticOffsetY, 0, highlightSyllableId);
		}

		public int SyllableCount => _syllableCount;
	}

	public abstract class LyricsItem
	{
		protected LyricsItem(string text) { Text = text; }

		public string Text { get; }

		public float TextWidth { get; private set; }

		public float AdvanceWidth { get; internal set; }

		public float Left { get; internal set; }

		public static void MeasureItems(Graphics graphics, IReadOnlyList<LyricsItem> items, Font font)
		{
			if (items.Count <= 0)
				return;
			List<CharacterRange> ranges = new List<CharacterRange>();
			StringBuilder sb = new StringBuilder();
			foreach (var item in items)
			{
				ranges.Add(new CharacterRange(sb.Length, item.Text.Length));
				sb.Append(item.Text);
			}
			var rects = graphics.MeasureCharacterRanges(sb.ToString(), font, ranges);
			foreach (var (item, rect) in Enumerable.Zip(items, rects, (x, y) => (x, y)))
				item.TextWidth = rect.Width;
		}

		public virtual void Measure(Graphics graphics, Font mainFont, Font phoneticFont) { }

		public virtual void Draw(Graphics graphics, Font mainFont, Font phoneticFont, Brush brush, float left, float bottom, float phoneticOffsetY)
		{
			graphics.DrawString(Text, mainFont, brush, left + Left + (AdvanceWidth - TextWidth) / 2, bottom - mainFont.Height, StringFormat.GenericTypographic);
		}

		public abstract void DrawHighlight(Graphics graphics, Font mainFont, Font phoneticFont, Brush brush, float left, float bottom, float phoneticOffsetY, float additionalHeight, int highlightSyllableId);
	}

	public class LyricsCharacterItem : LyricsItem
	{
		public LyricsCharacterItem(string text, int? syllableId) : base(text) { SyllableIdentifier = syllableId; }

		public int? SyllableIdentifier { get; }

		public override void DrawHighlight(Graphics graphics, Font mainFont, Font phoneticFont, Brush brush, float left, float bottom, float phoneticOffsetY, float additionalHeight, int highlightSyllableId)
		{
			if (SyllableIdentifier == highlightSyllableId)
				graphics.FillRectangle(brush, left + Left, bottom - mainFont.Height, AdvanceWidth, mainFont.Height + additionalHeight);
		}
	}

	public class LyricsCompositeItem : LyricsItem
	{
		public LyricsCompositeItem(string text, IEnumerable<LyricsCharacterItem> phonetic) : base(text) { Phonetic = new ReadOnlyCollection<LyricsCharacterItem>(phonetic.ToArray()); }

		public ReadOnlyCollection<LyricsCharacterItem> Phonetic { get; }

		public override void Measure(Graphics graphics, Font mainFont, Font phoneticFont)
		{
			MeasureItems(graphics, Phonetic, phoneticFont);
			AdvanceWidth = Math.Max(Phonetic.Sum(x => x.TextWidth), AdvanceWidth);
			for (int i = 0; i < Phonetic.Count; i++)
			{
				Phonetic[i].AdvanceWidth = AdvanceWidth / Phonetic.Count;
				Phonetic[i].Left = Left + i * AdvanceWidth / Phonetic.Count;
			}
		}

		public override void Draw(Graphics graphics, Font mainFont, Font phoneticFont, Brush brush, float left, float bottom, float phoneticOffsetY)
		{
			// ふりがなの描画
			for (int i = 0; i < Phonetic.Count; i++)
				graphics.DrawString(Phonetic[i].Text, phoneticFont, brush, left + Phonetic[i].Left + (Phonetic[i].AdvanceWidth - Phonetic[i].TextWidth) / 2, bottom - mainFont.Height - phoneticOffsetY - phoneticFont.Height, StringFormat.GenericTypographic);
			// ベーステキストの描画
			base.Draw(graphics, mainFont, phoneticFont, brush, left, bottom, phoneticOffsetY);
		}

		public override void DrawHighlight(Graphics graphics, Font mainFont, Font phoneticFont, Brush brush, float left, float bottom, float phoneticOffsetY, float additionalHeight, int highlightSyllableId)
		{
			for (int i = 0; i < Phonetic.Count; i++)
				Phonetic[i].DrawHighlight(graphics, phoneticFont, null, brush, left, bottom - mainFont.Height - phoneticOffsetY, 0, additionalHeight + phoneticOffsetY + mainFont.Height, highlightSyllableId);
		}
	}
}
