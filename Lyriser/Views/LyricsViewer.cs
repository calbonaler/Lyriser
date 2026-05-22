using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Lyriser.Models;

namespace Lyriser.Views;

public class LyricsViewer : FrameworkElement, IScrollInfo
{
	public LyricsViewer()
	{
		Focusable = true;
		UseLayoutRounding = true;
	}

	FontFamily FontFamily { get; } = new FontFamily("Meiryo");

	static FontWeight FontWeight => FontWeights.Normal;
	static FontStyle FontStyle => FontStyles.Normal;
	static FontStretch FontStretch => FontStretches.Normal;
	static double FontSize => 14.0 * 96.0 / 72.0;

	static Thickness MainPadding => new(10, 5, 10, 5);
	static Thickness NextPadding => MainPadding;

	[Flags]
	enum InvalidatedItems
	{
		None = 0,
		Source = 1,
		CurrentSyllable = 2,
		All = Source | CurrentSyllable,
	}

	static Core.DirectWrite.Factory? _directWriteFactory;
	static Core.DirectWrite.Factory DirectWriteFactory => _directWriteFactory ??= new();
	InvalidatedItems _invalidatedItems = InvalidatedItems.All;
	DrawingVisual[] _lineVisuals = [];
	DrawingVisual? _nextLineBackgroundVisual;
	DrawingVisual? _nextLineVisual;
	DrawingVisual? _highlightVisual;

	public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
		nameof(Source), typeof(LyricsSource), typeof(LyricsViewer),
		new PropertyMetadata(LyricsSource.Empty, (s, e) => ((LyricsViewer)s).OnSourceChanged(e)));
	public static readonly DependencyProperty CurrentSyllableProperty = DependencyProperty.Register(
		nameof(CurrentSyllable), typeof(SyllableLocation), typeof(LyricsViewer),
		new FrameworkPropertyMetadata(default(SyllableLocation), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
			(s, e) => ((LyricsViewer)s).OnCurrentSyllableChanged(e)));

	protected virtual void OnSourceChanged(DependencyPropertyChangedEventArgs e)
	{
		CoerceHighlight();
		_invalidatedItems = InvalidatedItems.Source;
		InvalidateMeasure();
	}
	protected virtual void OnCurrentSyllableChanged(DependencyPropertyChangedEventArgs e)
	{
		_invalidatedItems = InvalidatedItems.CurrentSyllable;
		InvalidateArrange();
	}

	public void HighlightNext(bool forward)
	{
		if (Source.SyllableLines.Count <= 0)
			return;
		var newLineIndex = CurrentSyllable.Line;
		var newColumnIndex = CurrentSyllable.Column + (forward ? 1 : -1);
		if (newColumnIndex >= 0 && newColumnIndex < Source.SyllableLines[newLineIndex].Syllables.Count)
			CurrentSyllable = new SyllableLocation(newLineIndex, newColumnIndex);
		else
		{
			newLineIndex += forward ? 1 : -1;
			if (newLineIndex >= 0 && newLineIndex < Source.SyllableLines.Count)
			{
				newColumnIndex = forward ? 0 : Source.SyllableLines[newLineIndex].Syllables.Count - 1;
				CurrentSyllable = new SyllableLocation(newLineIndex, newColumnIndex);
			}
		}
		ScrollIntoCurrentSyllable();
	}
	public void HighlightNextLine(bool forward)
	{
		if (Source.SyllableLines.Count <= 0)
			return;
		var syllableLine = Source.SyllableLines[CurrentSyllable.Line];
		var subSyllables = syllableLine.Syllables[CurrentSyllable.Column];
		using var lineRun = new TextRun(DirectWriteFactory, Source.PhysicalLines[syllableLine.PhysicalLineIndex], FontFamily, FontWeight, FontStyle, FontStretch, FontSize);
		var centerX = (lineRun.GetSubSyllableBounds(subSyllables[0]).Left + lineRun.GetSubSyllableBounds(subSyllables.Last()).Right) / 2;
		var newLineIndex = CurrentSyllable.Line + (forward ? 1 : -1);
		if (newLineIndex >= 0 && newLineIndex < Source.SyllableLines.Count)
		{
			using var nextLineRun = new TextRun(DirectWriteFactory, Source.PhysicalLines[Source.SyllableLines[newLineIndex].PhysicalLineIndex], FontFamily, FontWeight, FontStyle, FontStretch, FontSize);
			CurrentSyllable = new SyllableLocation(newLineIndex, FindNearestSyllableIndex(newLineIndex, centerX, nextLineRun));
			ScrollIntoSyllable(CurrentSyllable, nextLineRun);
		}
		else
			ScrollIntoCurrentSyllable();
	}
	public void HighlightFirst()
	{
		CurrentSyllable = new SyllableLocation(0, 0);
		ScrollOffset = new Vector(0, 0);
	}
	public void HighlightLast()
	{
		if (Source.SyllableLines.Count <= 0)
			return;
		CurrentSyllable = new SyllableLocation(Source.SyllableLines.Count - 1, Source.SyllableLines[^1].Syllables.Count - 1);
		ScrollOffset = (Vector)ExtentSize - (Vector)ViewportSize;
	}
	void CoerceHighlight()
	{
		if (Source.SyllableLines.Count <= 0)
			return;
		var line = CurrentSyllable.Line;
		var column = CurrentSyllable.Column;
		if (line >= Source.SyllableLines.Count)
			line = Source.SyllableLines.Count - 1;
		if (column >= Source.SyllableLines[line].Syllables.Count)
			column = Source.SyllableLines[line].Syllables.Count - 1;
		CurrentSyllable = new SyllableLocation(line, column);
	}
	int FindNearestLineIndex(double y)
	{
		var distance = double.PositiveInfinity;
		var candidateLineIndex = -1;
		for (var i = 0; i < Source.SyllableLines.Count; i++)
		{
			var dist = Math.Abs(y - (Source.SyllableLines[i].PhysicalLineIndex + 0.5) * TextRun.GetLineHeight(FontSize));
			if (dist < distance)
			{
				distance = dist;
				candidateLineIndex = i;
			}
		}
		return candidateLineIndex;
	}
	int FindNearestSyllableIndex(int lineIndex, double x, TextRun? lineRun)
	{
		var distance = double.PositiveInfinity;
		var nearestIndex = 0;
		TextRun? allocatedRun = null;
		try
		{
			lineRun ??= allocatedRun = new TextRun(DirectWriteFactory, Source.PhysicalLines[Source.SyllableLines[lineIndex].PhysicalLineIndex], FontFamily, FontWeight, FontStyle, FontStretch, FontSize);
			for (var i = 0; i < Source.SyllableLines[lineIndex].Syllables.Count; i++)
			{
				foreach (var subSyllable in Source.SyllableLines[lineIndex].Syllables[i])
				{
					var bounds = lineRun.GetSubSyllableBounds(subSyllable);
					var dist = Math.Abs(x - (bounds.Left + bounds.Right) / 2);
					if (dist < distance)
						(distance, nearestIndex) = (dist, i);
				}
			}
		}
		finally { allocatedRun?.Dispose(); }
		return nearestIndex;
	}
	SyllableLocation HitTestPoint(Point point)
	{
		var lineIndex = FindNearestLineIndex(point.Y);
		return lineIndex < 0 ? new SyllableLocation(0, 0) : new SyllableLocation(lineIndex, FindNearestSyllableIndex(lineIndex, point.X, null));
	}
	public void ScrollIntoCurrentSyllable() => ScrollIntoSyllable(CurrentSyllable, null);
	void ScrollIntoSyllable(SyllableLocation syllableLocation, TextRun? lineRun)
	{
		var syllableLine = Source.SyllableLines[syllableLocation.Line];
		Rect[] rects;
		TextRun? allocatedRun = null;
		try
		{
			lineRun ??= allocatedRun = new TextRun(DirectWriteFactory, Source.PhysicalLines[syllableLine.PhysicalLineIndex], FontFamily, FontWeight, FontStyle, FontStretch, FontSize);
			rects = [.. syllableLine.Syllables[syllableLocation.Column].Select(lineRun.GetSubSyllableBounds)];
		}
		finally {  allocatedRun?.Dispose(); }
		_ = MakeVisible(_lineVisuals[syllableLine.PhysicalLineIndex], new Rect(
			new Point(
				rects.Min(x => x.Left) - MainPadding.Left,
				-MainPadding.Top
			),
			new Point(
				rects.Max(x => x.Right) + MainPadding.Right,
				TextRun.GetLineHeight(FontSize) + MainPadding.Bottom
			)
		), new Size(ViewportSize.Width, ViewportSize.Height - NextLineViewerHeight));
	}

	public LyricsSource Source
	{
		get => (LyricsSource)GetValue(SourceProperty);
		set => SetValue(SourceProperty, value);
	}
	public SyllableLocation CurrentSyllable
	{
		get => (SyllableLocation)GetValue(CurrentSyllableProperty);
		set => SetValue(CurrentSyllableProperty, value);
	}
	static double NextLineViewerHeight => NextPadding.Top + TextRun.GetLineHeight(FontSize) + NextPadding.Bottom;

	protected override int VisualChildrenCount => (_highlightVisual != null ? 1 : 0) + _lineVisuals.Length + (_nextLineBackgroundVisual != null ? 1 : 0) + (_nextLineVisual != null ? 1 : 0);
	protected override Visual GetVisualChild(int index)
	{
		ArgumentOutOfRangeException.ThrowIfNegative(index);
		ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, VisualChildrenCount);
		if (_highlightVisual != null)
		{
			if (index == 0)
				return _highlightVisual;
			index--;
		}
		if (index < _lineVisuals.Length)
			return _lineVisuals[index];
		index -= _lineVisuals.Length;
		if (_nextLineBackgroundVisual != null)
		{
			if (index == 0)
				return _nextLineBackgroundVisual;
		}
		return _nextLineVisual!;
	}

	void ArrangeChildVisuals(Size finalSize)
	{
		var pixelsPerDip = (float)VisualTreeHelper.GetDpi(this).PixelsPerDip;
		if ((_invalidatedItems & InvalidatedItems.Source) != 0)
		{
			foreach (var visual in _lineVisuals)
				RemoveVisualChild(visual);
			var newLineVisuals = _lineVisuals;
			_lineVisuals = [];
			CreateLineVisuals(ref newLineVisuals, pixelsPerDip);
			foreach (var visual in newLineVisuals)
				AddVisualChild(visual);
			_lineVisuals = newLineVisuals;
		}
		for (var i = 0; i < _lineVisuals.Length; i++)
			_lineVisuals[i].Offset = new Vector(MainPadding.Left, MainPadding.Top + i * TextRun.GetLineHeight(FontSize)) - ScrollOffset;
		SetVisualChild(ref _nextLineBackgroundVisual, null);
		SetVisualChild(ref _nextLineBackgroundVisual, CreateNextLineBackgroundVisual(finalSize));
		if ((_invalidatedItems & InvalidatedItems.Source) != 0 || (_invalidatedItems & InvalidatedItems.CurrentSyllable) != 0)
		{
			SetVisualChild(ref _nextLineVisual, null);
			SetVisualChild(ref _nextLineVisual, CreateNextLineVisual(pixelsPerDip));
		}
		if (_nextLineVisual != null)
			_nextLineVisual.Offset = new Vector(NextPadding.Left - ScrollOffset.X, finalSize.Height - NextPadding.Bottom - TextRun.GetLineHeight(FontSize));
		if ((_invalidatedItems & InvalidatedItems.Source) != 0 || (_invalidatedItems & InvalidatedItems.CurrentSyllable) != 0)
		{
			SetVisualChild(ref _highlightVisual, null);
			SetVisualChild(ref _highlightVisual, CreateHighlightVisual());
		}
		if (_highlightVisual != null)
			_highlightVisual.Offset = new Vector(MainPadding.Left, MainPadding.Top + Source.SyllableLines[CurrentSyllable.Line].PhysicalLineIndex * TextRun.GetLineHeight(FontSize)) - ScrollOffset;
		_invalidatedItems = InvalidatedItems.None;
	}
	void CreateLineVisuals(ref DrawingVisual[] newLineVisuals, float pixelsPerDip)
	{
		if (newLineVisuals.Length != Source.PhysicalLines.Count)
			newLineVisuals = new DrawingVisual[Source.PhysicalLines.Count];
		for (var i = 0; i < Source.PhysicalLines.Count; i++)
		{
			var visual = new DrawingVisual();
			using (var dc = visual.RenderOpen())
			{
				dc.PushGuidelineSet(new(null, [TextRun.GetBaseline(FontSize)]));
				try
				{
					using var run = new TextRun(DirectWriteFactory, Source.PhysicalLines[i], FontFamily, FontWeight, FontStyle, FontStretch, FontSize);
					run.Draw(new TextRendererImpl(dc, Brushes.Black, pixelsPerDip));
				}
				finally { dc.Pop(); }
			}
			newLineVisuals[i] = visual;
		}
	}
	static DrawingVisual CreateNextLineBackgroundVisual(Size finalSize)
	{
		var visual = new DrawingVisual();
		using (var dc = visual.RenderOpen())
		{
			dc.PushGuidelineSet(new(null, [finalSize.Height - NextLineViewerHeight]));
			try { dc.DrawRectangle(Brushes.Gray, null, new Rect(0, finalSize.Height - NextLineViewerHeight, finalSize.Width, NextLineViewerHeight)); }
			finally { dc.Pop(); }
		}
		return visual;
	}
	DrawingVisual? CreateNextLineVisual(float pixelsPerDip)
	{
		if (CurrentSyllable.Line + 1 >= Source.SyllableLines.Count) return null;
		var visual = new DrawingVisual();
		using (var dc = visual.RenderOpen())
		{
			dc.PushGuidelineSet(new(null, [TextRun.GetBaseline(FontSize)]));
			try
			{
				using var run = new TextRun(DirectWriteFactory,
					Source.PhysicalLines[Source.SyllableLines[CurrentSyllable.Line + 1].PhysicalLineIndex],
					FontFamily, FontWeight, FontStyle, FontStretch, FontSize);
				run.Draw(new TextRendererImpl(dc, Brushes.White, pixelsPerDip));
			}
			finally { dc.Pop(); }
		}
		return visual;
	}
	DrawingVisual? CreateHighlightVisual()
	{
		if (Source.SyllableLines.Count <= 0) return null;
		var visual = new DrawingVisual();
		using (var dc = visual.RenderOpen())
		using (var run = new TextRun(DirectWriteFactory,
			Source.PhysicalLines[Source.SyllableLines[CurrentSyllable.Line].PhysicalLineIndex],
			FontFamily, FontWeight, FontStyle, FontStretch, FontSize))
		{
			foreach (var subSyllable in Source.SyllableLines[CurrentSyllable.Line].Syllables[CurrentSyllable.Column])
				dc.DrawRectangle(Brushes.Cyan, null, run.GetSubSyllableBounds(subSyllable));
		}
		return visual;
	}
	void SetVisualChild<T>(ref T? visual, T? newVisual) where T : Visual
	{
		if (visual != null)
			RemoveVisualChild(visual);
		visual = newVisual;
		if (visual != null)
			AddVisualChild(visual);
	}

	protected override Size MeasureOverride(Size availableSize)
	{
		if ((_invalidatedItems & InvalidatedItems.Source) != 0)
		{
			ExtentSize = new Size(
				Math.Max(MainPadding.Left, NextPadding.Left) +
					Source.PhysicalLines.Select(x =>
					{
						using var run = new TextRun(DirectWriteFactory, x, FontFamily, FontWeight, FontStyle, FontStretch, FontSize);
						return run.WidthIncludingTrailingWhitespace;
					}).DefaultIfEmpty().Max() +
					Math.Max(MainPadding.Right, NextPadding.Right),
				MainPadding.Top
					+ Source.PhysicalLines.Count * TextRun.GetLineHeight(FontSize)
					+ MainPadding.Bottom
			);
		}
		return new Size(Math.Min(ExtentSize.Width, availableSize.Width), Math.Min(ExtentSize.Height, availableSize.Height));
	}
	protected override Size ArrangeOverride(Size finalSize)
	{
		ViewportSize = new(finalSize.Width, finalSize.Height - NextPadding.Top - TextRun.GetLineHeight(FontSize) - NextPadding.Bottom);
		ArrangeChildVisuals(finalSize);
		return finalSize;
	}

	//protected override void OnForeColorChanged(EventArgs e)
	//{
	//	base.OnForeColorChanged(e);
	//	if (m_TextBrush != null)
	//		m_TextBrush.Color = ForeColor.ToColor4();
	//}
	//protected override void OnFontChanged(EventArgs e)
	//{
	//	base.OnFontChanged(e);
	//	_run.ChangeFont(_writeFactory, Font);
	//	_nextRun.ChangeFont(_writeFactory, Font);
	//}
	protected override void OnRender(DrawingContext dc)
	{
		base.OnRender(dc);
		dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, ActualWidth, ActualHeight));
	}
	protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
	{
		base.OnMouseLeftButtonDown(e);
		Focus();
		CurrentSyllable = HitTestPoint(e.GetPosition(this) + ScrollOffset - new Vector(MainPadding.Left, MainPadding.Top));
	}
	protected override void OnKeyDown(KeyEventArgs e)
	{
		base.OnKeyDown(e);
		if (e.Key == Key.Left)
		{
			if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
				HighlightFirst();
			else
				HighlightNext(false);
		}
		else if (e.Key == Key.Right)
		{
			if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
				HighlightLast();
			else
				HighlightNext(true);
		}
		else if (e.Key is Key.Up or Key.Down)
			HighlightNextLine(e.Key == Key.Down);
		e.Handled = true;
	}

	public ScrollViewer? ScrollOwner { get; set; }
	public bool CanHorizontallyScroll { get => true; set { if (!value) throw new ArgumentException($"cannot set {nameof(CanHorizontallyScroll)} to false.", nameof(value)); } }
	public bool CanVerticallyScroll { get => true; set { if (!value) throw new ArgumentException($"cannot set {nameof(CanVerticallyScroll)} to false.", nameof(value)); } }

	Size _extentSize;
	Size ExtentSize
	{
		get => _extentSize;
		set
		{
			_extentSize = value;
			ScrollOwner?.InvalidateScrollInfo();
		}
	}
	Size _viewportSize;
	Size ViewportSize
	{
		get => _viewportSize;
		set
		{
			_viewportSize = value;
			ScrollOwner?.InvalidateScrollInfo();
		}
	}
	Vector _scrollOffset;
	Vector ScrollOffset
	{
		get => _scrollOffset;
		set
		{
			_scrollOffset = new Vector(
				Math.Max(Math.Min(value.X, ExtentSize.Width - ViewportSize.Width), 0),
				Math.Max(Math.Min(value.Y, ExtentSize.Height - ViewportSize.Height), 0)
			);
			InvalidateArrange();
			ScrollOwner?.InvalidateScrollInfo();
		}
	}
	public double ExtentWidth => ExtentSize.Width;
	public double ExtentHeight => ExtentSize.Height;
	public double ViewportWidth => ViewportSize.Width;
	public double ViewportHeight => ViewportSize.Height;
	public double HorizontalOffset => ScrollOffset.X;
	public double VerticalOffset => ScrollOffset.Y;
	static double RoundDownToNearestLine(double size)
	{
		var lineSize = TextRun.GetLineHeight(FontSize);
		return Math.Truncate(size / lineSize) * lineSize;
	}

	void ScrollHorizontally(double delta) => ScrollOffset += new Vector(delta, 0);
	void ScrollVertically(double delta) => ScrollOffset += new Vector(0, delta);
	public void LineLeft() => ScrollHorizontally(-TextRun.GetLineHeight(FontSize));
	public void LineRight() => ScrollHorizontally(TextRun.GetLineHeight(FontSize));
	public void LineUp() => ScrollVertically(-TextRun.GetLineHeight(FontSize));
	public void LineDown() => ScrollVertically(TextRun.GetLineHeight(FontSize));
	public void MouseWheelLeft() => LineLeft();
	public void MouseWheelRight() => LineRight();
	public void MouseWheelUp() => LineUp();
	public void MouseWheelDown() => LineDown();
	public void PageLeft() => ScrollHorizontally(-RoundDownToNearestLine(ViewportSize.Width));
	public void PageRight() => ScrollHorizontally(RoundDownToNearestLine(ViewportSize.Width));
	public void PageUp() => ScrollVertically(-RoundDownToNearestLine(ViewportSize.Height));
	public void PageDown() => ScrollVertically(RoundDownToNearestLine(ViewportSize.Height));
	public void SetHorizontalOffset(double offset) => ScrollOffset = ScrollOffset with { X = offset };
	public void SetVerticalOffset(double offset) => ScrollOffset = ScrollOffset with { Y = offset };
	Rect MakeVisible(Visual visual, Rect rectangle, Size viewportSize)
	{
		static double ComputeScrollOffset(double viewportStart, double viewportLength, double rectStart, double rectLength)
			=> rectStart < viewportStart ? rectStart :
				rectStart + rectLength > viewportStart + viewportLength ? rectStart + rectLength - viewportLength :
				viewportStart;

		if (rectangle.IsEmpty || visual == null || visual != this && !IsAncestorOf(visual))
			return Rect.Empty;
		var rectangleInViewportCoord = visual.TransformToAncestor(this).TransformBounds(rectangle);
		var viewportInExtentCoord = new Rect((Point)ScrollOffset, viewportSize);
		var rectangleInExtentCoord = Rect.Offset(rectangleInViewportCoord, ScrollOffset);
		var minX = ComputeScrollOffset(viewportInExtentCoord.X, viewportInExtentCoord.Width, rectangleInExtentCoord.X, rectangleInExtentCoord.Width);
		var minY = ComputeScrollOffset(viewportInExtentCoord.Y, viewportInExtentCoord.Height, rectangleInExtentCoord.Y, rectangleInExtentCoord.Height);
		ScrollOffset = new Vector(minX, minY);
		var scrolledViewport = new Rect((Point)ScrollOffset, viewportSize);
		var visibleRect = Rect.Intersect(rectangleInExtentCoord, scrolledViewport);
		return !visibleRect.IsEmpty ? Rect.Offset(visibleRect, -ScrollOffset) : visibleRect;
	}
	public Rect MakeVisible(Visual visual, Rect rectangle) => MakeVisible(visual, rectangle, ViewportSize);
}

abstract class Attached(Core.DirectWrite.TextRange range)
{
	public Core.DirectWrite.TextRange Range { get; } = range;

	public abstract void Draw(Core.DirectWrite.ITextRenderer renderer);
	public abstract double Measure(Core.DirectWrite.TextLayout baseTextLayout);
	public abstract void Arrange(Core.DirectWrite.TextLayout baseTextLayout);
	public abstract Rect GetSubSyllableBounds(Core.DirectWrite.TextLayout baseTextLayout, int textPosition);

	protected Core.DirectWrite.HitTestMetrics GetMetricsForRange(Core.DirectWrite.TextLayout baseTextLayout)
	{
		var result = baseTextLayout.HitTestTextRange(Range, default, out var rangeMetrics);
		Debug.Assert(result, "All base characters specified by single ruby group must be same script.");
		return rangeMetrics;
	}
}

class Ruby(Core.DirectWrite.Factory factory, Core.DirectWrite.TextRange range, string text, Core.DirectWrite.TextFormat format) : Attached(range), IDisposable
{
	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}
	protected virtual void Dispose(bool disposing)
	{
		if (!disposing) return;
		_textLayout.Dispose();
	}

	Vector _origin;
	readonly Core.DirectWrite.TextLayout _textLayout = factory.CreateTextLayout(text, format, default);

	public string Text { get; } = text;

	public override void Draw(Core.DirectWrite.ITextRenderer textRenderer) => _textLayout.Draw(textRenderer, _origin.ToDWrite());
	public override double Measure(Core.DirectWrite.TextLayout baseTextLayout)
	{
		return Math.Max((double)_textLayout.Metrics.Width - GetMetricsForRange(baseTextLayout).Size.X, 0) / 2;
	}
	public override void Arrange(Core.DirectWrite.TextLayout baseTextLayout)
	{
		var rangeMetrics = GetMetricsForRange(baseTextLayout);
		var nonWhitespaceClusterCount = _textLayout.GetClusterMetrics().Count(x => !x.IsWhitespace);
		var spacing = (double)rangeMetrics.Size.X - _textLayout.Metrics.Width;
		_textLayout.MaxWidth = (float)(rangeMetrics.Size.X - spacing / nonWhitespaceClusterCount);
		_textLayout.TextAlignment = Core.DirectWrite.TextAlignment.Justified;
		var textWidth = _textLayout.GetClusterMetrics().Select(x => x.Width).RobustSum();
		if (textWidth < _textLayout.MaxWidth)
		{
			// text does not seem to be justified, so we use centering instead
			_textLayout.TextAlignment = Core.DirectWrite.TextAlignment.Center;
		}
		_origin = new(rangeMetrics.TopLeft.X + spacing / 2 / nonWhitespaceClusterCount, rangeMetrics.TopLeft.Y);
	}
	public override Rect GetSubSyllableBounds(Core.DirectWrite.TextLayout baseTextLayout, int textPosition)
	{
		var metrics = GetMetricsForRange(baseTextLayout);
		var (bounds, range) = GetCharacterBounds(textPosition);
		return new Rect(
			new Point(
				range.StartPosition > 0 ? bounds.TopLeft.X : metrics.TopLeft.X,
				bounds.TopLeft.Y
			),
			new Point(
				range.StartPosition + range.Length < Text.Length ? bounds.BottomRight.X : metrics.BottomRight.X,
				metrics.BottomRight.Y
			)
		);
	}

	(Rect Value, Core.DirectWrite.TextRange Range) GetCharacterBounds(int textPosition)
	{
		var (_, metrics) = _textLayout.HitTestTextPosition(textPosition, false);
		var bounds = new Rect(metrics.TopLeft.ToWpfPoint(), metrics.Size.ToWpfSize());
		bounds.Offset(_origin);
		return (bounds, metrics.TextRange);
	}
}

class SyllableDivision(Core.DirectWrite.TextRange range, int divisionCount) : Attached(range)
{
	public int DivisionCount { get; } = divisionCount;

	public override void Draw(Core.DirectWrite.ITextRenderer textRenderer) { }
	public override double Measure(Core.DirectWrite.TextLayout baseTextLayout) => 0.0f;
	public override void Arrange(Core.DirectWrite.TextLayout baseTextLayout) { }
	public override Rect GetSubSyllableBounds(Core.DirectWrite.TextLayout baseTextLayout, int textPosition)
	{
		var rangeMetrics = GetMetricsForRange(baseTextLayout);
		var (_, metrics) = baseTextLayout.HitTestTextPosition(Range.StartPosition, false);
		return new(rangeMetrics.TopLeft.X + (double)rangeMetrics.Size.X * textPosition / DivisionCount, metrics.TopLeft.Y,
			(double)rangeMetrics.Size.X / DivisionCount, (double)rangeMetrics.BottomRight.Y - metrics.TopLeft.Y);
	}
}

class TextRun : IDisposable
{
	public TextRun(Core.DirectWrite.Factory writeFactory, PhysicalLine physicalLine, FontFamily fontFamily, FontWeight fontWeight, FontStyle fontStyle, FontStretch fontStretch, double fontSize)
	{
		var fontFamilyName = fontFamily.Source;
		var dwriteFontWeight = fontWeight.ToOpenTypeWeight();
		var dwriteFontStyle = fontStyle.ToDWrite();
		var dwriteFontStretch = fontStretch.ToOpenTypeStretch();
		using (var format = writeFactory.CreateTextFormat(fontFamilyName, dwriteFontWeight, dwriteFontStyle, dwriteFontStretch, (float)fontSize))
		{
			format.WordWrapping = Core.DirectWrite.WordWrapping.NoWrap;
			format.LineSpacing = GetLineSpacing(fontSize);
			_textLayout = writeFactory.CreateTextLayout(physicalLine.Text, format, default);
		}
		_attacheds = new Attached[physicalLine.AttachedSpecifiers.Count];
		using (var format = writeFactory.CreateTextFormat(fontFamilyName, dwriteFontWeight, dwriteFontStyle, dwriteFontStretch, (float)GetRubyFontSize(fontSize)))
		{
			format.WordWrapping = Core.DirectWrite.WordWrapping.NoWrap;
			for (var i = 0; i < physicalLine.AttachedSpecifiers.Count; i++)
			{
				_attacheds[i] = physicalLine.AttachedSpecifiers[i] switch
				{
					RubySpecifier rubySpecifier => new Ruby(writeFactory, rubySpecifier.Range, rubySpecifier.Text, format),
					_ => new SyllableDivision(((SyllableDivisionSpecifier)physicalLine.AttachedSpecifiers[i]).Range, ((SyllableDivisionSpecifier)physicalLine.AttachedSpecifiers[i]).DivisionCount),
				};
				var spacing = _attacheds[i].Measure(_textLayout);
				SetRangeSpacing(spacing, spacing, 0, _attacheds[i].Range);
				_attacheds[i].Arrange(_textLayout);
			}
		}
	}
	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}
	protected virtual void Dispose(bool disposing)
	{
		if (!disposing)
			return;
		_textLayout.Dispose();
		foreach (var item in _attacheds)
		{
			if (item is IDisposable disposable)
				disposable.Dispose();
		}
		_attacheds = [];
	}

	readonly Core.DirectWrite.TextLayout _textLayout;
	Attached[] _attacheds = [];

	static double GetRubyFontSize(double fontSize) => fontSize / 2;
	static Core.DirectWrite.LineSpacingSet GetLineSpacing(double fontSize)
	{
		var baseline = fontSize + GetRubyFontSize(fontSize);
		return new(Core.DirectWrite.LineSpacingMethod.Uniform, (float)(baseline * (1.0 / 0.8)), (float)baseline);
	}
	public static double GetBaseline(double fontSize) => GetLineSpacing(fontSize).Baseline;
	public static double GetLineHeight(double fontSize) => GetLineSpacing(fontSize).LineSpacing;

	public double WidthIncludingTrailingWhitespace => _textLayout.Metrics.WidthIncludingTrailingWhitespace;

	public void Draw(Core.DirectWrite.ITextRenderer textRenderer)
	{
		foreach (var it in _attacheds)
			it.Draw(textRenderer);
		_textLayout.Draw(textRenderer, default);
	}
	public Rect GetSubSyllableBounds(SubSyllable subSyllable)
	{
		if (subSyllable.IsSimple)
		{
			// HitTestTextPoint retrieves actual character bounds, while HitTestTextRange retrieves line-based bounds.
			var (_, metrics) = _textLayout.HitTestTextPosition(subSyllable.CharacterIndex, false);
			var result = _textLayout.HitTestTextRange(metrics.TextRange, default, out var rangeMetrics);
			Debug.Assert(result, "One index must reference one script group.");
			return new(new Point(rangeMetrics.TopLeft.X, metrics.TopLeft.Y), rangeMetrics.BottomRight.ToWpfPoint());
		}
		else
			return _attacheds[subSyllable.AttachedIndex].GetSubSyllableBounds(_textLayout, subSyllable.CharacterIndex);
	}

	void SetRangeSpacing(double leadingSpacing, double trailingSpacing, double minimumAdvanceWidth, Core.DirectWrite.TextRange range)
	{
		var metricsForRange = new List<(Core.DirectWrite.TextRange Range, bool IsRightToLeft)>();
		var clusters = _textLayout.GetClusterMetrics();
		var start = 0;
		foreach (var cluster in clusters)
		{
			var newRangeStart = Math.Max(start, range.StartPosition);
			var newRangeEnd = Math.Min(start + cluster.Length, range.StartPosition + range.Length);
			if (newRangeEnd > newRangeStart)
			{
				metricsForRange.Add((Core.DirectWrite.TextRange.FromStartEnd(newRangeStart, newRangeEnd), cluster.IsRightToLeft));
				if (start + cluster.Length >= range.StartPosition + range.Length) break;
			}
			start += cluster.Length;
		}
		if (metricsForRange.Count == 1)
			_textLayout.SetCharacterSpacing((float)leadingSpacing, (float)trailingSpacing, (float)minimumAdvanceWidth, range);
		else
		{
			var (leading, trailing) = (leadingSpacing, 0.0);
			if (metricsForRange[0].IsRightToLeft)
				(leading, trailing) = (trailing, leading);
			_textLayout.SetCharacterSpacing((float)leading, (float)trailing, (float)minimumAdvanceWidth, metricsForRange[0].Range);

			(leading, trailing) = (0.0, trailingSpacing);
			if (metricsForRange.Last().IsRightToLeft)
				(leading, trailing) = (trailing, leading);
			_textLayout.SetCharacterSpacing((float)leading, (float)trailing, (float)minimumAdvanceWidth, metricsForRange.Last().Range);
		}
	}
}

class TextRendererImpl(DrawingContext drawingContext, Brush foregroundBrush, float pixelsPerDip) : Core.DirectWrite.ITextRenderer
{
	readonly DrawingContext _drawingContext = drawingContext;
	readonly Brush _foregroundBrush = foregroundBrush;

	public bool IsPixelSnappingDisabled => false;
	public System.Numerics.Matrix3x2 CurrentTransform => System.Numerics.Matrix3x2.Identity;
	public float PixelsPerDip { get; } = pixelsPerDip;

	public void DrawGlyphRun(System.Numerics.Vector2 baselineOrigin, Core.DirectWrite.MeasuringMode measuringMode, in Core.DirectWrite.GlyphRun glyphRun, in Core.DirectWrite.GlyphRunDescription glyphRunDescription)
	{
		static TOut[] Convert<TIn, TOut>(ReadOnlySpan<TIn> span, Func<TIn, TOut> converter)
		{
			var array = new TOut[span.Length];
			for (var i = 0; i < array.Length; i++)
				array[i] = converter(span[i]);
			return array;
		}

		if (glyphRun.GlyphIndices.IsEmpty)
			return;
		GlyphTypeface glyphTypeface;
		using (var fontFace = glyphRun.FetchFontFace())
		{
			var typeface = new Typeface(
				new FontFamily(fontFace.GetFirstFamilyName()),
				fontFace.Style.ToWpf(),
				FontWeight.FromOpenTypeWeight(fontFace.Weight),
				FontStretch.FromOpenTypeStretch(fontFace.Stretch)
			);
			if (!typeface.TryGetGlyphTypeface(out glyphTypeface))
				return;
		}
		_drawingContext.DrawGlyphRun(_foregroundBrush,
			new GlyphRun(glyphTypeface, glyphRun.BidiLevel, glyphRun.IsSideways, glyphRun.FontEmSize, PixelsPerDip,
				glyphRun.GlyphIndices.ToArray(),
				baselineOrigin.ToWpfPoint(),
				Convert(glyphRun.GlyphAdvances, x => (double)x),
				glyphRun.GlyphOffsets.IsEmpty ? null : Convert(glyphRun.GlyphOffsets, x => new Point(x.AdvanceOffset, x.AscenderOffset)),
				null, null, null, null, null
			)
		);
	}
}

static class ConversionUtils
{
	public static Point ToWpfPoint(this System.Numerics.Vector2 vector) => new(vector.X, vector.Y);
	public static Size ToWpfSize(this System.Numerics.Vector2 vector) => new(vector.X, vector.Y);
	public static System.Numerics.Vector2 ToDWrite(this Vector vector) => new((float)vector.X, (float)vector.Y);

	public static Core.DirectWrite.FontStyle ToDWrite(this FontStyle fontStyle) =>
		  fontStyle == FontStyles.Italic ? Core.DirectWrite.FontStyle.Italic
		: fontStyle == FontStyles.Oblique ? Core.DirectWrite.FontStyle.Oblique
		: Core.DirectWrite.FontStyle.Normal;
	public static FontStyle ToWpf(this Core.DirectWrite.FontStyle fontStyle) => fontStyle switch
	{
		Core.DirectWrite.FontStyle.Italic => FontStyles.Italic,
		Core.DirectWrite.FontStyle.Oblique => FontStyles.Oblique,
		_ => FontStyles.Normal,
	};
}

static class FloatUtils
{
	/// <summary>
	/// 中間値を使用して完全精度の総和をとります。
	/// これはPythonで書かれた https://code.activestate.com/recipes/393090/ のmsum関数のC#移植版です。
	/// </summary>
	/// <param name="values">総和をとる値</param>
	/// <returns>総和</returns>
	public static float RobustSum(this IEnumerable<float> values)
	{
		var partials = new List<float>();
		foreach (var xi in values)
		{
			var x = xi;
			var index = 0;
			for (var i = 0; i < partials.Count; ++i)
			{
				var y = partials[i];
				if (Math.Abs(x) < Math.Abs(y))
					(x, y) = (y, x);
				var hi = x + y;
				var lo = y - (hi - x);
				if (lo != 0.0)
				{
					partials[index] = lo;
					index++;
				}
				x = hi;
			}
			if (index < partials.Count)
			{
				partials[index] = x;
				partials.RemoveRange(index + 1, partials.Count - index - 1);
			}
			else
			{
				partials.Add(x);
			}
		}
		var result = 0.0f;
		foreach (var x in partials)
			result += x;
		return result;
	}
}
