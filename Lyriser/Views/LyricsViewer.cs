using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Lyriser.Models;

namespace Lyriser.Views;

public class LyricsViewer : FrameworkElement
{
	public LyricsViewer()
	{
		Loaded += OnLoaded;
		Unloaded += OnUnloaded;
		Focusable = true;
		UseLayoutRounding = true;
	}

	FontFamily FontFamily { get; } = new FontFamily("Meiryo");

	static FontWeight FontWeight => FontWeights.Normal;
	static FontStyle FontStyle => FontStyles.Normal;
	static FontStretch FontStretch => FontStretches.Normal;
	static double FontSize => 14.0 * 96.0 / 72.0;

	static Thickness MainPadding => new(10, 5, 10, 5);
	const double NextTopPadding = 5;
	const double NextBottomPadding = 5;

	Core.DirectWrite.Factory? _writeFactory;
	TextRun? _run;
	TextRun? _nextRun;

	public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
		nameof(Source), typeof(LyricsSource), typeof(LyricsViewer),
		new PropertyMetadata(LyricsSource.Empty, (s, e) => ((LyricsViewer)s).OnSourceChanged(e)));
	public static readonly DependencyProperty CurrentSyllableProperty = DependencyProperty.Register(
		nameof(CurrentSyllable), typeof(SyllableLocation), typeof(LyricsViewer),
		new FrameworkPropertyMetadata(default(SyllableLocation), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
			(s, e) => ((LyricsViewer)s).OnCurrentSyllableChanged(e)));
	public static readonly DependencyProperty ScrollPositionXProperty = DependencyProperty.Register(
		nameof(ScrollPositionX), typeof(double), typeof(LyricsViewer),
		new PropertyMetadata(0.0, (s, e) => ((LyricsViewer)s).OnScrollPositionXChanged(e),
			(s, v) => Math.Clamp((double)v, 0, ((LyricsViewer)s).ScrollMaximumX)));
	public static readonly DependencyProperty ScrollPositionYProperty = DependencyProperty.Register(
		nameof(ScrollPositionY), typeof(double), typeof(LyricsViewer),
		new PropertyMetadata(0.0, (s, e) => ((LyricsViewer)s).OnScrollPositionYChanged(e),
			(s, v) => Math.Clamp((double)v, 0, ((LyricsViewer)s).ScrollMaximumY)));
	static readonly DependencyPropertyKey ScrollMaximumXPropertyKey = DependencyProperty.RegisterReadOnly(nameof(ScrollMaximumX), typeof(double), typeof(LyricsViewer), new PropertyMetadata());
	static readonly DependencyPropertyKey ScrollMaximumYPropertyKey = DependencyProperty.RegisterReadOnly(nameof(ScrollMaximumY), typeof(double), typeof(LyricsViewer), new PropertyMetadata());
	public static readonly DependencyProperty ScrollMaximumXProperty = ScrollMaximumXPropertyKey.DependencyProperty;
	public static readonly DependencyProperty ScrollMaximumYProperty = ScrollMaximumYPropertyKey.DependencyProperty;

	void OnLoaded(object sender, RoutedEventArgs e)
	{
		_writeFactory = new Core.DirectWrite.Factory();
		_run = new TextRun(_writeFactory, FontFamily, FontWeight, FontStyle, FontStretch, FontSize);
		_nextRun = new TextRun(_writeFactory, FontFamily, FontWeight, FontStyle, FontStretch, FontSize);
		InvalidateVisual();
	}
	void OnUnloaded(object sender, RoutedEventArgs e)
	{
		Utils.SafeDispose(ref _run);
		Utils.SafeDispose(ref _nextRun);
		Utils.SafeDispose(ref _writeFactory);
	}

	protected virtual void OnSourceChanged(DependencyPropertyChangedEventArgs e)
	{
		Debug.Assert(_writeFactory != null && _run != null, "not initialized");
		_run.SetUp(_writeFactory, Source.Text, Source.AttachedSpecifiers, FontFamily, FontWeight, FontStyle, FontStretch, FontSize);
		UpdateNextLineViewer();
		UpdateScrollInfo();
		CoerceHighlight();
		InvalidateVisual();
	}
	protected virtual void OnCurrentSyllableChanged(DependencyPropertyChangedEventArgs e)
	{
		UpdateNextLineViewer();
		InvalidateVisual();
	}
	protected virtual void OnScrollPositionXChanged(DependencyPropertyChangedEventArgs e) => InvalidateVisual();
	protected virtual void OnScrollPositionYChanged(DependencyPropertyChangedEventArgs e) => InvalidateVisual();

	public void HighlightNext(bool forward)
	{
		if (Source.SyllableLines.Count <= 0)
			return;
		var newLineIndex = CurrentSyllable.Line;
		var newColumnIndex = CurrentSyllable.Column + (forward ? 1 : -1);
		if (newColumnIndex >= 0 && newColumnIndex < Source.SyllableLines[newLineIndex].Length)
			CurrentSyllable = new SyllableLocation(newLineIndex, newColumnIndex);
		else
		{
			newLineIndex += forward ? 1 : -1;
			if (newLineIndex >= 0 && newLineIndex < Source.SyllableLines.Count)
			{
				newColumnIndex = forward ? 0 : Source.SyllableLines[newLineIndex].Length - 1;
				CurrentSyllable = new SyllableLocation(newLineIndex, newColumnIndex);
			}
		}
		ScrollIntoCurrentSyllable();
	}
	public void HighlightNextLine(bool forward)
	{
		if (Source.SyllableLines.Count <= 0)
			return;
		Debug.Assert(_run != null, "not initialized");
		var subSyllables = Source.SyllableLines[CurrentSyllable.Line][CurrentSyllable.Column];
		var centerX = (_run.GetSubSyllableBounds(subSyllables[0]).TopLeft.X + _run.GetSubSyllableBounds(subSyllables.Last()).BottomRight.X) / 2;
		var newLineIndex = CurrentSyllable.Line + (forward ? 1 : -1);
		if (newLineIndex >= 0 && newLineIndex < Source.SyllableLines.Count)
			CurrentSyllable = new SyllableLocation(newLineIndex, FindNearestSyllableIndex(newLineIndex, centerX));
		ScrollIntoCurrentSyllable();
	}
	public void HighlightFirst()
	{
		CurrentSyllable = new SyllableLocation(0, 0);
		ScrollPositionX = 0;
		ScrollPositionY = 0;
	}
	public void HighlightLast()
	{
		if (Source.SyllableLines.Count <= 0)
			return;
		CurrentSyllable = new SyllableLocation(Source.SyllableLines.Count - 1, Source.SyllableLines[^1].Length - 1);
		ScrollPositionX = ScrollMaximumX;
		ScrollPositionY = ScrollMaximumY;
	}
	void CoerceHighlight()
	{
		if (Source.SyllableLines.Count <= 0)
			return;
		var line = CurrentSyllable.Line;
		var column = CurrentSyllable.Column;
		if (line >= Source.SyllableLines.Count)
			line = Source.SyllableLines.Count - 1;
		if (column >= Source.SyllableLines[line].Length)
			column = Source.SyllableLines[line].Length - 1;
		CurrentSyllable = new SyllableLocation(line, column);
	}
	void UpdateNextLineViewer()
	{
		Debug.Assert(_writeFactory != null && _run != null && _nextRun != null, "not initialized");
		if (CurrentSyllable.Line + 1 < Source.SyllableLines.Count)
		{
			var (text, attachedSpecs) = _run.GetLine(CurrentSyllable.Line + 1, Source.LineMap);
			_nextRun.SetUp(_writeFactory, text, attachedSpecs, FontFamily, FontWeight, FontStyle, FontStretch, FontSize);
		}
		else
			_nextRun.SetUp(_writeFactory, string.Empty, [], FontFamily, FontWeight, FontStyle, FontStretch, FontSize);
	}
	int FindNearestLineIndex(double y)
	{
		Debug.Assert(_run != null, "not initialized");
		var distance = double.PositiveInfinity;
		var candidateLineIndex = -1;
		for (var i = 0; i < Source.SyllableLines.Count; i++)
		{
			var bounds = _run.GetSubSyllableBounds(Source.SyllableLines[i][0][0]);
			var dist = Math.Abs(y + _run.LineSpacing.LineSpacing / 2 - bounds.BottomRight.Y);
			if (dist < distance)
			{
				distance = dist;
				candidateLineIndex = i;
			}
		}
		return candidateLineIndex;
	}
	int FindNearestSyllableIndex(int lineIndex, double x)
	{
		Debug.Assert(_run != null, "not initialized");
		var distance = double.PositiveInfinity;
		var nearestIndex = 0;
		for (var i = 0; i < Source.SyllableLines[lineIndex].Length; i++)
		{
			foreach (var subSyllable in Source.SyllableLines[lineIndex][i])
			{
				var bounds = _run.GetSubSyllableBounds(subSyllable);
				var dist = Math.Abs(x - (bounds.TopLeft.X + bounds.BottomRight.X) / 2);
				if (dist < distance)
					(distance, nearestIndex) = (dist, i);
			}
		}
		return nearestIndex;
	}
	SyllableLocation HitTestPoint(Point point)
	{
		var lineIndex = FindNearestLineIndex(point.Y);
		return lineIndex < 0 ? new SyllableLocation(0, 0) : new SyllableLocation(lineIndex, FindNearestSyllableIndex(lineIndex, point.X));
	}
	public void ScrollIntoCurrentSyllable() => ScrollInto(Source.SyllableLines[CurrentSyllable.Line][CurrentSyllable.Column]);
	void ScrollInto(IEnumerable<SubSyllable> syllable)
	{
		Debug.Assert(_run != null, "not initialized");
		var rects = syllable.Select(_run.GetSubSyllableBounds).ToArray();
		ScrollInto(new Rect(
			new Point(
				rects.Min(x => x.TopLeft.X) - MainPadding.Left,
				rects[0].BottomRight.Y - _run.LineSpacing.LineSpacing - MainPadding.Top
			),
			new Point(
				rects.Max(x => x.BottomRight.X) + MainPadding.Right,
				rects[0].BottomRight.Y + MainPadding.Bottom
			)
		));
	}
	void ScrollInto(Rect bounds)
	{
		var topLeft = default(Point) - ViewportOffset;
		var bottomRight = new Point(ActualWidth, ActualHeight - NextLineViewerHeight) - ViewportOffset;

		double offsetX = 0;
		if (bounds.TopLeft.X < topLeft.X)
			offsetX += bounds.TopLeft.X - topLeft.X;
		else if (bounds.BottomRight.X > bottomRight.X)
			offsetX += bounds.BottomRight.X - bottomRight.X;
		double offsetY = 0;
		if (bounds.TopLeft.Y < topLeft.Y)
			offsetY += bounds.TopLeft.Y - topLeft.Y;
		else if (bounds.BottomRight.Y > bottomRight.Y)
			offsetY += bounds.BottomRight.Y - bottomRight.Y;

		ScrollPositionX += offsetX;
		ScrollPositionY += offsetY;
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
	public double ScrollPositionX
	{
		get => (double)GetValue(ScrollPositionXProperty);
		set => SetValue(ScrollPositionXProperty, value);
	}
	public double ScrollPositionY
	{
		get => (double)GetValue(ScrollPositionYProperty);
		set => SetValue(ScrollPositionYProperty, value);
	}
	public double ScrollMaximumX
	{
		get => (double)GetValue(ScrollMaximumXProperty);
		private set => SetValue(ScrollMaximumXPropertyKey, value);
	}
	public double ScrollMaximumY
	{
		get => (double)GetValue(ScrollMaximumYProperty);
		private set => SetValue(ScrollMaximumYPropertyKey, value);
	}
	Vector ViewportOffset => new(-ScrollPositionX + MainPadding.Left, -ScrollPositionY + MainPadding.Top);
	double NextLineViewerHeight
	{
		get
		{
			Debug.Assert(_nextRun != null, "not initialized");
			return NextTopPadding + _nextRun.Size.Height + NextBottomPadding;
		}
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
		if (_run == null || _nextRun == null)
			return;

		var pixelsPerDip = (float)VisualTreeHelper.GetDpi(this).PixelsPerDip;

		dc.PushClip(new RectangleGeometry(new Rect(0, 0, ActualWidth, ActualHeight)));
		dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, ActualWidth, ActualHeight));
		dc.PushTransform(new TranslateTransform(ViewportOffset.X, ViewportOffset.Y));
		if (Source != null && Source.SyllableLines.Count > 0)
		{
			foreach (var subSyllable in Source.SyllableLines[CurrentSyllable.Line][CurrentSyllable.Column])
				dc.DrawRectangle(Brushes.Cyan, null, _run.GetSubSyllableBounds(subSyllable));
		}
		_run.Draw(new TextRendererImpl(dc, Brushes.Black, ViewportOffset, pixelsPerDip));
		dc.Pop();

		dc.DrawRectangle(Brushes.Gray, null, new Rect(0, ActualHeight - NextLineViewerHeight, ActualWidth, NextLineViewerHeight));
		dc.PushTransform(new TranslateTransform(ViewportOffset.X, ActualHeight - NextLineViewerHeight + NextTopPadding));
		_nextRun.Draw(new TextRendererImpl(dc, Brushes.White, new Vector(ViewportOffset.X, ActualHeight - NextLineViewerHeight + NextTopPadding), pixelsPerDip));
		dc.Pop();
		dc.Pop();
	}
	protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
	{
		base.OnRenderSizeChanged(sizeInfo);
		UpdateScrollInfo();
		InvalidateVisual();
	}
	protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
	{
		base.OnMouseLeftButtonDown(e);
		Focus();
		CurrentSyllable = HitTestPoint(e.GetPosition(this) - ViewportOffset);
	}
	protected override void OnMouseWheel(MouseWheelEventArgs e)
	{
		base.OnMouseWheel(e);
		Debug.Assert(_run != null, "not initialized");
		var offset = -e.Delta / Mouse.MouseWheelDeltaForOneLine * _run.LineSpacing.LineSpacing;
		ScrollPositionY += offset;
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

	void UpdateScrollInfo()
	{
		if (_writeFactory == null || _run == null || _nextRun == null)
			return;
		var size = _run.Size;
		// Add paddings
		size.Width += MainPadding.Left + MainPadding.Right;
		size.Height += MainPadding.Top + MainPadding.Bottom;
		// Add next line viewer height
		size.Height += NextLineViewerHeight;
		ScrollMaximumX = Math.Max(size.Width - ActualWidth, 0.0);
		ScrollMaximumY = Math.Max(size.Height - ActualHeight, 0.0);
	}
}

abstract class Attached(Core.DirectWrite.TextRange range)
{
	public Core.DirectWrite.TextRange Range { get; } = range;

	public abstract void Recreate(Core.DirectWrite.Factory factory, Core.DirectWrite.TextFormat format);
	public abstract void Draw(Core.DirectWrite.ITextRenderer renderer);
	public abstract double Measure(Core.DirectWrite.TextLayout baseTextLayout);
	public abstract void Arrange(Core.DirectWrite.TextLayout baseTextLayout);
	public abstract Rect GetSubSyllableBounds(Core.DirectWrite.TextLayout baseTextLayout, int textPosition);
	public abstract AttachedSpecifier CreateSpecifier();

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
	Core.DirectWrite.TextLayout _textLayout = factory.CreateTextLayout(text, format, default);

	public string Text { get; } = text;

	public override void Recreate(Core.DirectWrite.Factory factory, Core.DirectWrite.TextFormat format)
	{
		_textLayout.Dispose();
		_textLayout = factory.CreateTextLayout(Text, format, default);
	}
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
	public override AttachedSpecifier CreateSpecifier() => new RubySpecifier(Range, Text);

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

	public override void Recreate(Core.DirectWrite.Factory factory, Core.DirectWrite.TextFormat format) { }
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
	public override AttachedSpecifier CreateSpecifier() => new SyllableDivisionSpecifier(Range, DivisionCount);
}

class TextRun : IDisposable
{
	public TextRun(Core.DirectWrite.Factory writeFacotry, FontFamily fontFamily, FontWeight fontWeight, FontStyle fontStyle, FontStretch fontStretch, double fontSize)
		=> SetUp(writeFacotry, string.Empty, [], fontFamily, fontWeight, fontStyle, fontStretch, fontSize);
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
		CleanupAttacheds();
	}

	string _text = string.Empty;
	Core.DirectWrite.TextLayout _textLayout;
	Attached[] _attacheds = [];

	public Size Size
	{
		get
		{
			var metrics = _textLayout.Metrics;
			return new(
				Math.Max(metrics.LayoutSize.X, metrics.WidthIncludingTrailingWhitespace),
				Math.Max(metrics.LayoutSize.Y, metrics.Height)
			);
		}
	}
	public Core.DirectWrite.LineSpacingSet LineSpacing => _textLayout.LineSpacing;
	public void Draw(Core.DirectWrite.ITextRenderer textRenderer)
	{
		foreach (var it in _attacheds)
			it.Draw(textRenderer);
		_textLayout.Draw(textRenderer, default);
	}
	[MemberNotNull(nameof(_textLayout))]
	public void SetUp(Core.DirectWrite.Factory writeFactory, string text, IReadOnlyList<AttachedSpecifier> attachedSpecifiers, FontFamily fontFamily, FontWeight fontWeight, FontStyle fontStyle, FontStretch fontStretch, double fontSize)
	{
		var fontFamilyName = fontFamily.Source;
		var dwriteFontWeight = fontWeight.ToOpenTypeWeight();
		var dwriteFontStyle = fontStyle.ToDWrite();
		var dwriteFontStretch = fontStretch.ToOpenTypeStretch();
		_text = text;
		using (var format = writeFactory.CreateTextFormat(fontFamilyName, dwriteFontWeight, dwriteFontStyle, dwriteFontStretch, (float)fontSize))
		{
			format.WordWrapping = Core.DirectWrite.WordWrapping.NoWrap;
			format.LineSpacing = new(Core.DirectWrite.LineSpacingMethod.Uniform, (float)(fontSize * 1.5 * (1.0 / 0.8)), (float)(fontSize * 1.5));
			_textLayout?.Dispose();
			_textLayout = writeFactory.CreateTextLayout(_text, format, default);
		}
		CleanupAttacheds();
		_attacheds = new Attached[attachedSpecifiers.Count];
		using (var format = writeFactory.CreateTextFormat(fontFamilyName, dwriteFontWeight, dwriteFontStyle, dwriteFontStretch, (float)(fontSize / 2)))
		{
			format.WordWrapping = Core.DirectWrite.WordWrapping.NoWrap;
			for (var i = 0; i < attachedSpecifiers.Count; i++)
			{
				_attacheds[i] = attachedSpecifiers[i] switch
				{
					RubySpecifier rubySpecifier => new Ruby(writeFactory, rubySpecifier.Range, rubySpecifier.Text, format),
					_ => new SyllableDivision(((SyllableDivisionSpecifier)attachedSpecifiers[i]).Range, ((SyllableDivisionSpecifier)attachedSpecifiers[i]).DivisionCount),
				};
				var spacing = _attacheds[i].Measure(_textLayout);
				SetRangeSpacing(spacing, spacing, 0, _attacheds[i].Range);
				_attacheds[i].Arrange(_textLayout);
			}
		}
	}
	public void ChangeFont(Core.DirectWrite.Factory writeFactory, FontFamily fontFamily, FontWeight fontWeight, FontStyle fontStyle, FontStretch fontStretch, double fontSize)
	{
		var fontFamilyName = fontFamily.Source;
		var dwriteFontWeight = fontWeight.ToOpenTypeWeight();
		var dwriteFontStyle = fontStyle.ToDWrite();
		var dwriteFontStretch = fontStretch.ToOpenTypeStretch();
		using (var format = writeFactory.CreateTextFormat(fontFamilyName, dwriteFontWeight, dwriteFontStyle, dwriteFontStretch, (float)fontSize))
		{
			format.WordWrapping = Core.DirectWrite.WordWrapping.NoWrap;
			format.LineSpacing = new(Core.DirectWrite.LineSpacingMethod.Uniform, (float)(fontSize * 1.5 * (1.0 / 0.8)), (float)(fontSize * 1.5));
			_textLayout.Dispose();
			_textLayout = writeFactory.CreateTextLayout(_text, format, default);
		}
		using (var format = writeFactory.CreateTextFormat(fontFamilyName, dwriteFontWeight, dwriteFontStyle, dwriteFontStretch, (float)(fontSize / 2)))
		{
			format.WordWrapping = Core.DirectWrite.WordWrapping.NoWrap;
			foreach (var attached in _attacheds)
			{
				attached.Recreate(writeFactory, format);
				var spacing = attached.Measure(_textLayout);
				SetRangeSpacing(spacing, spacing, 0, attached.Range);
				attached.Arrange(_textLayout);
			}
		}
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
	public (string Text, AttachedSpecifier[] AttachedSpecifiers) GetLine(int lineIndex, LineMap lineMap)
	{
		var physicalLine = lineMap.GetPhysicalLineByLogical(lineIndex);
		var text = _text.Substring(physicalLine.TextStart, physicalLine.TextLength).TrimEnd('\n');
		var attachedSpecs = _attacheds.Skip(physicalLine.AttachedStart).Take(physicalLine.AttachedLength).Select(x => x.CreateSpecifier().Move(-physicalLine.TextStart)).ToArray();
		return (text, attachedSpecs);
	}

	void CleanupAttacheds()
	{
		foreach (var item in _attacheds)
		{
			if (item is IDisposable disposable)
				disposable.Dispose();
		}
		_attacheds = [];
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

class TextRendererImpl(DrawingContext drawingContext, Brush foregroundBrush, Vector translateTransform, float pixelsPerDip) : Core.DirectWrite.ITextRenderer
{
	readonly DrawingContext _drawingContext = drawingContext;
	readonly Brush _foregroundBrush = foregroundBrush;
	readonly Vector _translateTransform = translateTransform;

	public bool IsPixelSnappingDisabled => false;
	public System.Numerics.Matrix3x2 CurrentTransform => System.Numerics.Matrix3x2.CreateTranslation(_translateTransform.ToDWrite());
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
		static Vector Round(Vector vector) => new(Math.Round(vector.X), Math.Round(vector.Y));

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
				(Point)(Round(((baselineOrigin + _translateTransform.ToDWrite()) * PixelsPerDip).ToWpfVector()) / PixelsPerDip - _translateTransform),
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
	public static Vector ToWpfVector(this System.Numerics.Vector2 vector) => new(vector.X, vector.Y);
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
