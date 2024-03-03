using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using System.Windows;
using System.Windows.Input;
using Lyriser.Models;

namespace Lyriser.Views;

public class LyricsViewer : D2dControl
{
	public LyricsViewer()
	{
		ResourceCache.Add("TextBrush", rt => rt.CreateSolidColorBrush(new Core.Direct2D1.ColorF(0x000000)));
		ResourceCache.Add("NextTextBrush", rt => rt.CreateSolidColorBrush(new Core.Direct2D1.ColorF(0xFFFFFF)));
		ResourceCache.Add("HighlightBrush", rt => rt.CreateSolidColorBrush(new Core.Direct2D1.ColorF(0x00FFFF)));
		Loaded += OnLoaded;
		Unloaded += OnUnloaded;
		Focusable = true;
	}

	System.Windows.Media.FontFamily FontFamily { get; } = new System.Windows.Media.FontFamily("Meiryo");

	static FontWeight FontWeight => FontWeights.Normal;
	static FontStyle FontStyle => FontStyles.Normal;
	static FontStretch FontStretch => FontStretches.Normal;
	static double FontSize => 14.0 * 96.0 / 72.0;

	const float LeftPadding = 10;
	const float TopPadding = 5;
	const float RightPadding = 10;
	const float BottomPadding = 5;
	const float NextTopPadding = 5;
	const float NextBottomPadding = 5;

	Core.DirectWrite.Factory? _writeFactory;
	TextRun? _run;
	TextRun? _nextRun;

	public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(nameof(Source), typeof(LyricsSource), typeof(LyricsViewer), new PropertyMetadata(LyricsSource.Empty, (s, e) => ((LyricsViewer)s).OnSourceChanged(e)));
	public static readonly DependencyProperty CurrentSyllableProperty = DependencyProperty.Register(
		nameof(CurrentSyllable), typeof(SyllableLocation), typeof(LyricsViewer),
		new FrameworkPropertyMetadata(default(SyllableLocation), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (s, e) => ((LyricsViewer)s).UpdateNextLineViewer()));
	public static readonly DependencyProperty ScrollPositionXProperty = DependencyProperty.Register(nameof(ScrollPositionX), typeof(double), typeof(LyricsViewer), new PropertyMetadata(0.0, null, (s, v) => Math.Clamp((double)v, 0, ((LyricsViewer)s).ScrollMaximumX)));
	public static readonly DependencyProperty ScrollPositionYProperty = DependencyProperty.Register(nameof(ScrollPositionY), typeof(double), typeof(LyricsViewer), new PropertyMetadata(0.0, null, (s, v) => Math.Clamp((double)v, 0, ((LyricsViewer)s).ScrollMaximumY)));
	static readonly DependencyPropertyKey ScrollMaximumXPropertyKey = DependencyProperty.RegisterReadOnly(nameof(ScrollMaximumX), typeof(double), typeof(LyricsViewer), new PropertyMetadata());
	static readonly DependencyPropertyKey ScrollMaximumYPropertyKey = DependencyProperty.RegisterReadOnly(nameof(ScrollMaximumY), typeof(double), typeof(LyricsViewer), new PropertyMetadata());
	public static readonly DependencyProperty ScrollMaximumXProperty = ScrollMaximumXPropertyKey.DependencyProperty;
	public static readonly DependencyProperty ScrollMaximumYProperty = ScrollMaximumYPropertyKey.DependencyProperty;

	void OnLoaded(object sender, RoutedEventArgs e)
	{
		_writeFactory = new Core.DirectWrite.Factory();
		_run = new TextRun(_writeFactory, FontFamily, FontWeight, FontStyle, FontStretch, FontSize);
		_nextRun = new TextRun(_writeFactory, FontFamily, FontWeight, FontStyle, FontStretch, FontSize);
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
	}

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
		var centerX = (_run.GetSubSyllableBounds(subSyllables[0]).TopLeft.X + _run.GetSubSyllableBounds(subSyllables.Last()).BottomRight.X) / 2.0f;
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
	int FindNearestLineIndex(float y)
	{
		Debug.Assert(_run != null, "not initialized");
		var distance = float.PositiveInfinity;
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
	int FindNearestSyllableIndex(int lineIndex, float x)
	{
		Debug.Assert(_run != null, "not initialized");
		var distance = float.PositiveInfinity;
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
	SyllableLocation HitTestPoint(Vector2 point)
	{
		var lineIndex = FindNearestLineIndex(point.Y);
		return lineIndex < 0 ? new SyllableLocation(0, 0) : new SyllableLocation(lineIndex, FindNearestSyllableIndex(lineIndex, point.X));
	}
	public void ScrollIntoCurrentSyllable() => ScrollInto(Source.SyllableLines[CurrentSyllable.Line][CurrentSyllable.Column]);
	void ScrollInto(IEnumerable<SubSyllable> syllable)
	{
		Debug.Assert(_run != null, "not initialized");
		var rects = syllable.Select(_run.GetSubSyllableBounds).ToArray();
		ScrollInto(Core.Direct2D1.RectF.FromLTRB(
			rects.Min(x => x.TopLeft.X) - LeftPadding,
			rects[0].BottomRight.Y - _run.LineSpacing.LineSpacing - TopPadding,
			rects.Max(x => x.BottomRight.X) + RightPadding,
			rects[0].BottomRight.Y + BottomPadding
		));
	}
	void ScrollInto(Core.Direct2D1.RectF bounds)
	{
		Matrix3x2.Invert(ViewTransform, out var transform);
		var topLeft = Vector2.Transform(default, transform);
		var bottomRight = Vector2.Transform(new Vector2((float)ActualWidth, (float)ActualHeight - NextLineViewerHeight), transform);

		float offsetX = 0;
		if (bounds.TopLeft.X < topLeft.X)
			offsetX += bounds.TopLeft.X - topLeft.X;
		else if (bounds.BottomRight.X > bottomRight.X)
			offsetX += bounds.BottomRight.X - bottomRight.X;
		float offsetY = 0;
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
	Matrix3x2 ViewTransform => Matrix3x2.CreateTranslation(-(float)ScrollPositionX + LeftPadding, -(float)ScrollPositionY + TopPadding);
	float NextLineViewerHeight
	{
		get
		{
			Debug.Assert(_nextRun != null, "not initialized");
			return NextTopPadding + _nextRun.Size.Y + NextBottomPadding;
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
	public override void Render(Core.Direct2D1.RenderTarget target)
	{
		if (IsInDesignMode)
			return;
		Debug.Assert(_run != null && _nextRun != null, "not initialized");
		target.Clear(new Core.Direct2D1.ColorF(0xFFFFFF));
		var transform = ViewTransform;
		target.Transform = transform;
		if (Source != null && Source.SyllableLines.Count > 0)
		{
			foreach (var subSyllable in Source.SyllableLines[CurrentSyllable.Line][CurrentSyllable.Column])
				target.FillRectangle(_run.GetSubSyllableBounds(subSyllable), (Core.Direct2D1.Brush)ResourceCache["HighlightBrush"]);
		}
		_run.Draw(target, (Core.Direct2D1.Brush)ResourceCache["TextBrush"]);
		target.Transform = Matrix3x2.Identity;

		var size = target.Size;
		target.PushAxisAlignedClip(Core.Direct2D1.RectF.FromXYWH(0, size.Y - NextLineViewerHeight, size.X, NextLineViewerHeight), Core.Direct2D1.AntialiasMode.Aliased);
		target.Clear(new Core.Direct2D1.ColorF(0x808080));
		target.Transform = Matrix3x2.CreateTranslation(transform.Translation.X, size.Y - NextLineViewerHeight + NextTopPadding);
		_nextRun.Draw(target, (Core.Direct2D1.Brush)ResourceCache["NextTextBrush"]);
		target.PopAxisAlignedClip();
	}
	protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
	{
		base.OnRenderSizeChanged(sizeInfo);
		UpdateScrollInfo();
	}
	protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
	{
		base.OnMouseLeftButtonDown(e);
		Focus();
		Matrix3x2.Invert(ViewTransform, out var transform);
		var pt = e.GetPosition(this);
		CurrentSyllable = HitTestPoint(Vector2.Transform(new Vector2((float)pt.X, (float)pt.Y), transform));
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
		size.X += LeftPadding + RightPadding;
		size.Y += TopPadding + BottomPadding;
		// Add next line viewer height
		size.Y += NextLineViewerHeight;
		ScrollMaximumX = Math.Max(size.X - ActualWidth, 0.0);
		ScrollMaximumY = Math.Max(size.Y - ActualHeight, 0.0);
	}
}

abstract class Attached(Core.DirectWrite.TextRange range)
{
	public Core.DirectWrite.TextRange Range { get; } = range;

	public abstract void Recreate(Core.DirectWrite.Factory factory, Core.DirectWrite.TextFormat format);
	public abstract void Draw(Core.Direct2D1.RenderTarget renderTarget, Core.Direct2D1.Brush defaultFillBrush);
	public abstract float Measure(Core.DirectWrite.TextLayout baseTextLayout);
	public abstract void Arrange(Core.DirectWrite.TextLayout baseTextLayout);
	public abstract Core.Direct2D1.RectF GetSubSyllableBounds(Core.DirectWrite.TextLayout baseTextLayout, int textPosition);
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

	Vector2 _origin;
	Core.DirectWrite.TextLayout _textLayout = factory.CreateTextLayout(text, format, default);

	public string Text { get; } = text;

	public override void Recreate(Core.DirectWrite.Factory factory, Core.DirectWrite.TextFormat format)
	{
		_textLayout.Dispose();
		_textLayout = factory.CreateTextLayout(Text, format, default);
	}
	public override void Draw(Core.Direct2D1.RenderTarget renderTarget, Core.Direct2D1.Brush defaultFillBrush)
	{
		renderTarget.DrawTextLayout(_origin, _textLayout, defaultFillBrush);
	}
	public override float Measure(Core.DirectWrite.TextLayout baseTextLayout)
	{
		return Math.Max(_textLayout.Metrics.Width - GetMetricsForRange(baseTextLayout).Size.X, 0.0f) / 2;
	}
	public override void Arrange(Core.DirectWrite.TextLayout baseTextLayout)
	{
		var rangeMetrics = GetMetricsForRange(baseTextLayout);
		var nonWhitespaceClusterCount = _textLayout.GetClusterMetrics().Count(x => !x.IsWhitespace);
		var spacing = rangeMetrics.Size.X - _textLayout.Metrics.Width;
		_textLayout.MaxWidth = rangeMetrics.Size.X - spacing / nonWhitespaceClusterCount;
		_textLayout.TextAlignment = Core.DirectWrite.TextAlignment.Justified;
		var textWidth = _textLayout.GetClusterMetrics().Select(x => x.Width).RobustSum();
		if (textWidth < _textLayout.MaxWidth)
		{
			// text does not seem to be justified, so we use centering instead
			_textLayout.TextAlignment = Core.DirectWrite.TextAlignment.Center;
		}
		_origin = new Vector2(rangeMetrics.TopLeft.X + spacing / 2 / nonWhitespaceClusterCount, rangeMetrics.TopLeft.Y);
	}
	public override Core.Direct2D1.RectF GetSubSyllableBounds(Core.DirectWrite.TextLayout baseTextLayout, int textPosition)
	{
		var metrics = GetMetricsForRange(baseTextLayout);
		var (bounds, range) = GetCharacterBounds(textPosition);
		return Core.Direct2D1.RectF.FromLTRB(
			range.StartPosition > 0 ? bounds.TopLeft.X : metrics.TopLeft.X,
			bounds.TopLeft.Y,
			range.StartPosition + range.Length < Text.Length ? bounds.BottomRight.X : metrics.BottomRight.X,
			metrics.BottomRight.Y
		);
	}
	public override AttachedSpecifier CreateSpecifier() => new RubySpecifier(Range, Text);

	(Core.Direct2D1.RectF Value, Core.DirectWrite.TextRange Range) GetCharacterBounds(int textPosition)
	{
		var (_, metrics) = _textLayout.HitTestTextPosition(textPosition, false);
		var bounds = Core.Direct2D1.RectF.FromXYWH(metrics.TopLeft, metrics.Size);
		bounds.TopLeft += _origin;
		bounds.BottomRight += _origin;
		return (bounds, metrics.TextRange);
	}
}

class SyllableDivision(Core.DirectWrite.TextRange range, int divisionCount) : Attached(range)
{
	public int DivisionCount { get; } = divisionCount;

	public override void Recreate(Core.DirectWrite.Factory factory, Core.DirectWrite.TextFormat format) { }
	public override void Draw(Core.Direct2D1.RenderTarget renderTarget, Core.Direct2D1.Brush defaultFillBrush) { }
	public override float Measure(Core.DirectWrite.TextLayout baseTextLayout) => 0.0f;
	public override void Arrange(Core.DirectWrite.TextLayout baseTextLayout) { }
	public override Core.Direct2D1.RectF GetSubSyllableBounds(Core.DirectWrite.TextLayout baseTextLayout, int textPosition)
	{
		var rangeMetrics = GetMetricsForRange(baseTextLayout);
		var (_, metrics) = baseTextLayout.HitTestTextPosition(Range.StartPosition, false);
		return Core.Direct2D1.RectF.FromXYWH(rangeMetrics.TopLeft.X + rangeMetrics.Size.X * textPosition / DivisionCount, metrics.TopLeft.Y, rangeMetrics.Size.X / DivisionCount, rangeMetrics.BottomRight.Y - metrics.TopLeft.Y);
	}
	public override AttachedSpecifier CreateSpecifier() => new SyllableDivisionSpecifier(Range, DivisionCount);
}

class TextRun : IDisposable
{
	public TextRun(Core.DirectWrite.Factory writeFacotry, System.Windows.Media.FontFamily fontFamily, FontWeight fontWeight, FontStyle fontStyle, FontStretch fontStretch, double fontSize)
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

	public Vector2 Size
	{
		get
		{
			var metrics = _textLayout.Metrics;
			return new Vector2(
				Math.Max(metrics.LayoutSize.X, metrics.WidthIncludingTrailingWhitespace),
				Math.Max(metrics.LayoutSize.Y, metrics.Height)
			);
		}
	}
	public Core.DirectWrite.LineSpacingSet LineSpacing => _textLayout.LineSpacing;
	public void Draw(Core.Direct2D1.RenderTarget renderTarget, Core.Direct2D1.Brush defaultFillBrush)
	{
		foreach (var it in _attacheds)
			it.Draw(renderTarget, defaultFillBrush);
		renderTarget.DrawTextLayout(default, _textLayout, defaultFillBrush);
	}
	[MemberNotNull(nameof(_textLayout))]
	public void SetUp(Core.DirectWrite.Factory writeFactory, string text, IReadOnlyList<AttachedSpecifier> attachedSpecifiers, System.Windows.Media.FontFamily fontFamily, FontWeight fontWeight, FontStyle fontStyle, FontStretch fontStretch, double fontSize)
	{
		var fontFamilyName = fontFamily.Source;
		var dwriteFontWeight = fontWeight.ToOpenTypeWeight();
		var dwriteFontStyle = fontStyle.ToDWrite();
		var dwriteFontStretch = fontStretch.ToOpenTypeStretch();
		_text = text;
		using (var format = writeFactory.CreateTextFormat(fontFamilyName, dwriteFontWeight, dwriteFontStyle, dwriteFontStretch, (float)fontSize))
		{
			format.WordWrapping = Core.DirectWrite.WordWrapping.NoWrap;
			format.LineSpacing = new(Core.DirectWrite.LineSpacingMethod.Uniform, (float)fontSize * 1.5f * (1.0f / 0.8f), (float)fontSize * 1.5f);
			_textLayout?.Dispose();
			_textLayout = writeFactory.CreateTextLayout(_text, format, default);
		}
		CleanupAttacheds();
		_attacheds = new Attached[attachedSpecifiers.Count];
		using (var format = writeFactory.CreateTextFormat(fontFamilyName, dwriteFontWeight, dwriteFontStyle, dwriteFontStretch, (float)fontSize / 2))
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
	public void ChangeFont(Core.DirectWrite.Factory writeFactory, System.Windows.Media.FontFamily fontFamily, FontWeight fontWeight, FontStyle fontStyle, FontStretch fontStretch, double fontSize)
	{
		var fontFamilyName = fontFamily.Source;
		var dwriteFontWeight = fontWeight.ToOpenTypeWeight();
		var dwriteFontStyle = fontStyle.ToDWrite();
		var dwriteFontStretch = fontStretch.ToOpenTypeStretch();
		using (var format = writeFactory.CreateTextFormat(fontFamilyName, dwriteFontWeight, dwriteFontStyle, dwriteFontStretch, (float)fontSize))
		{
			format.WordWrapping = Core.DirectWrite.WordWrapping.NoWrap;
			format.LineSpacing = new(Core.DirectWrite.LineSpacingMethod.Uniform, (float)fontSize * 1.5f * (1.0f / 0.8f), (float)fontSize * 1.5f);
			_textLayout.Dispose();
			_textLayout = writeFactory.CreateTextLayout(_text, format, default);
		}
		using (var format = writeFactory.CreateTextFormat(fontFamilyName, dwriteFontWeight, dwriteFontStyle, dwriteFontStretch, (float)fontSize / 2))
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
	public Core.Direct2D1.RectF GetSubSyllableBounds(SubSyllable subSyllable)
	{
		if (subSyllable.IsSimple)
		{
			// HitTestTextPoint retrieves actual character bounds, while HitTestTextRange retrieves line-based bounds.
			var (_, metrics) = _textLayout.HitTestTextPosition(subSyllable.CharacterIndex, false);
			var result = _textLayout.HitTestTextRange(metrics.TextRange, default, out var rangeMetrics);
			Debug.Assert(result, "One index must reference one script group.");
			return Core.Direct2D1.RectF.FromLTRB(new Vector2(rangeMetrics.TopLeft.X, metrics.TopLeft.Y), rangeMetrics.BottomRight);
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
	void SetRangeSpacing(float leadingSpacing, float trailingSpacing, float minimumAdvanceWidth, Core.DirectWrite.TextRange range)
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
			_textLayout.SetCharacterSpacing(leadingSpacing, trailingSpacing, minimumAdvanceWidth, range);
		else
		{
			var (leading, trailing) = (leadingSpacing, 0.0f);
			if (metricsForRange[0].IsRightToLeft)
				(leading, trailing) = (trailing, leading);
			_textLayout.SetCharacterSpacing(leading, trailing, minimumAdvanceWidth, metricsForRange[0].Range);

			(leading, trailing) = (0.0f, trailingSpacing);
			if (metricsForRange.Last().IsRightToLeft)
				(leading, trailing) = (trailing, leading);
			_textLayout.SetCharacterSpacing(leading, trailing, minimumAdvanceWidth, metricsForRange.Last().Range);
		}
	}
}

static class DWriteExtensions
{
	public static Core.DirectWrite.FontStyle ToDWrite(this FontStyle fontStyle) =>
		  fontStyle == FontStyles.Italic ? Core.DirectWrite.FontStyle.Italic
		: fontStyle == FontStyles.Oblique ? Core.DirectWrite.FontStyle.Oblique
		: Core.DirectWrite.FontStyle.Normal;
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
