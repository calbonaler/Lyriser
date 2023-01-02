using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using System.Windows;
using System.Windows.Input;
using Lyriser.Models;

namespace Lyriser.Views
{
	public class LyricsViewer : D2dControl
	{
		public LyricsViewer()
		{
			ResourceCache.Add("TextBrush", rt => rt.CreateSolidColorBrush(new Core.ColorF(0x000000)));
			ResourceCache.Add("NextTextBrush", rt => rt.CreateSolidColorBrush(new Core.ColorF(0xFFFFFF)));
			ResourceCache.Add("HighlightBrush", rt => rt.CreateSolidColorBrush(new Core.ColorF(0x00FFFF)));
			Loaded += OnLoaded;
			Unloaded += OnUnloaded;
			Focusable = true;
		}

		System.Windows.Media.FontFamily FontFamily { get; } = new System.Windows.Media.FontFamily("Meiryo");

		static FontWeight FontWeight => FontWeights.Normal;
		static FontStyle FontStyle => FontStyles.Normal;
		static FontStretch FontStretch => FontStretches.Normal;
		static double FontSize => 14.0 * 96.0 / 72.0;

		const float s_LeftPadding = 10;
		const float s_TopPadding = 5;
		const float s_RightPadding = 10;
		const float s_BottomPadding = 5;
		const float s_NextTopPadding = 5;
		const float s_NextBottomPadding = 5;

		Core.DirectWrite.Factory? m_WriteFactory;
		TextRun? m_Run;
		NextTextRun? m_NextRun;

		public static readonly new DependencyProperty SourceProperty = DependencyProperty.Register(nameof(Source), typeof(LyricsSource), typeof(LyricsViewer), new PropertyMetadata(LyricsSource.Empty, (s, e) => ((LyricsViewer)s).OnSourceChanged(e)));
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
			m_WriteFactory = new Core.DirectWrite.Factory();
			m_Run = new TextRun(m_WriteFactory, FontFamily, FontWeight, FontStyle, FontStretch, FontSize);
			m_NextRun = new NextTextRun(m_WriteFactory, FontFamily, FontWeight, FontStyle, FontStretch, FontSize);
		}
		void OnUnloaded(object sender, RoutedEventArgs e)
		{
			Utils.SafeDispose(ref m_Run);
			Utils.SafeDispose(ref m_NextRun);
			Utils.SafeDispose(ref m_WriteFactory);
		}

		protected virtual void OnSourceChanged(DependencyPropertyChangedEventArgs e)
		{
			Debug.Assert(m_WriteFactory != null && m_Run != null && m_NextRun != null, "not initialized");
			m_Run.Setup(m_WriteFactory, Source.Text, Source.AttachedSpecifiers, FontFamily, FontWeight, FontStyle, FontStretch, FontSize);
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
			Debug.Assert(m_WriteFactory != null && m_Run != null && m_NextRun != null, "not initialized");
			var subSyllables = Source.SyllableLines[CurrentSyllable.Line][CurrentSyllable.Column];
			var centerX = (m_Run.GetSubSyllableBounds(subSyllables[0]).TopLeft.X + m_Run.GetSubSyllableBounds(subSyllables.Last()).BottomRight.X) / 2.0f;
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
			Debug.Assert(m_WriteFactory != null && m_Run != null && m_NextRun != null, "not initialized");
			if (CurrentSyllable.Line + 1 < Source.SyllableLines.Count)
			{
				var (text, attachedSpecs) = m_Run.GetLine(CurrentSyllable.Line + 1, Source.LineMap);
				m_NextRun.Setup(m_WriteFactory, text, attachedSpecs, FontFamily, FontWeight, FontStyle, FontStretch, FontSize);
			}
			else
				m_NextRun.Setup(m_WriteFactory, string.Empty, Array.Empty<AttachedSpecifier>(), FontFamily, FontWeight, FontStyle, FontStretch, FontSize);
		}
		int FindNearestLineIndex(float y)
		{
			Debug.Assert(m_WriteFactory != null && m_Run != null && m_NextRun != null, "not initialized");
			var distance = float.PositiveInfinity;
			var candidateLineIndex = -1;
			for (var i = 0; i < Source.SyllableLines.Count; i++)
			{
				var bounds = m_Run.GetSubSyllableBounds(Source.SyllableLines[i][0][0]);
				var dist = Math.Abs(y + m_Run.LineSpacing.LineSpacing / 2 - bounds.BottomRight.Y);
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
			Debug.Assert(m_WriteFactory != null && m_Run != null && m_NextRun != null, "not initialized");
			var distance = float.PositiveInfinity;
			var nearestIndex = 0;
			for (var i = 0; i < Source.SyllableLines[lineIndex].Length; i++)
			{
				foreach (var subSyllable in Source.SyllableLines[lineIndex][i])
				{
					var bounds = m_Run.GetSubSyllableBounds(subSyllable);
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
			Debug.Assert(m_WriteFactory != null && m_Run != null && m_NextRun != null, "not initialized");
			var rects = syllable.Select(x => m_Run.GetSubSyllableBounds(x)).ToArray();
			if (rects.Length <= 0)
				throw new ArgumentException("Syllable must contain at least one sub-syllable", nameof(syllable));
			ScrollInto(Core.Direct2D1.RectF.FromLTRB(
				rects.Min(x => x.TopLeft.X) - s_LeftPadding,
				rects[0].BottomRight.Y - m_Run.LineSpacing.LineSpacing - s_TopPadding,
				rects.Max(x => x.BottomRight.X) + s_RightPadding,
				rects[0].BottomRight.Y + s_BottomPadding
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

		public new LyricsSource Source
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
		Matrix3x2 ViewTransform => Matrix3x2.CreateTranslation(-(float)ScrollPositionX + s_LeftPadding, -(float)ScrollPositionY + s_TopPadding);
		float NextLineViewerHeight
		{
			get
			{
				Debug.Assert(m_WriteFactory != null && m_Run != null && m_NextRun != null, "not initialized");
				return s_NextTopPadding + m_NextRun.Size.Y + s_NextBottomPadding;
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
		//	m_Run.ChangeFont(m_WriteFactory, Font);
		//	m_NextRun.ChangeFont(m_WriteFactory, Font);
		//}
		public override void Render(Core.Direct2D1.RenderTarget target)
		{
			if (IsInDesignMode)
				return;
			Debug.Assert(m_WriteFactory != null && m_Run != null && m_NextRun != null, "not initialized");
			target.Clear(new Core.ColorF(0xFFFFFF));
			var transform = ViewTransform;
			target.Transform = transform;
			if (Source != null && Source.SyllableLines.Count > 0)
			{
				foreach (var subSyllable in Source.SyllableLines[CurrentSyllable.Line][CurrentSyllable.Column])
					target.FillRectangle(m_Run.GetSubSyllableBounds(subSyllable), (Core.Direct2D1.Brush)ResourceCache["HighlightBrush"]);
			}
			m_Run.Draw(target, (Core.Direct2D1.Brush)ResourceCache["TextBrush"]);
			target.Transform = Matrix3x2.Identity;

			var size = target.Size;
			target.PushAxisAlignedClip(Core.Direct2D1.RectF.FromXYWH(0, size.Y - NextLineViewerHeight, size.X, NextLineViewerHeight), Core.Direct2D1.AntialiasMode.Aliased);
			target.Clear(new Core.ColorF(0x808080));
			target.Transform = Matrix3x2.CreateTranslation(transform.Translation.X, size.Y - NextLineViewerHeight + s_NextTopPadding);
			m_NextRun.Draw(target, (Core.Direct2D1.Brush)ResourceCache["NextTextBrush"]);
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
			if (e == null)
				throw new ArgumentNullException(nameof(e));
			Focus();
			Matrix3x2.Invert(ViewTransform, out var transform);
			var pt = e.GetPosition(this);
			CurrentSyllable = HitTestPoint(Vector2.Transform(new Vector2((float)pt.X, (float)pt.Y), transform));
		}
		protected override void OnMouseWheel(MouseWheelEventArgs e)
		{
			base.OnMouseWheel(e);
			Debug.Assert(m_WriteFactory != null && m_Run != null && m_NextRun != null, "not initialized");
			var offset = -e.Delta / Mouse.MouseWheelDeltaForOneLine * m_Run.LineSpacing.LineSpacing;
			ScrollPositionY += offset;
		}
		protected override void OnKeyDown(KeyEventArgs e)
		{
			base.OnKeyDown(e);
			if (e == null)
				throw new ArgumentNullException(nameof(e));
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
			Debug.Assert(m_WriteFactory != null && m_Run != null && m_NextRun != null, "not initialized");
			var size = m_Run.Size;
			// Add paddings
			size.X += s_LeftPadding + s_RightPadding;
			size.Y += s_TopPadding + s_BottomPadding;
			// Add next line viewer height
			size.Y += NextLineViewerHeight;
			ScrollMaximumX = Math.Max(size.X - ActualWidth, 0.0);
			ScrollMaximumY = Math.Max(size.Y - ActualHeight, 0.0);
		}
	}

	abstract class Attached
	{
		protected Attached(Core.DirectWrite.TextRange range) => Range = range;

		public Core.DirectWrite.TextRange Range { get; }

		public abstract void Recreate(Core.DirectWrite.Factory factory, Core.DirectWrite.TextFormat format);
		public abstract void Draw(Core.Direct2D1.RenderTarget renderTarget, Core.Direct2D1.Brush defaultFillBrush);
		public abstract float Measure(Core.DirectWrite.TextLayout baseTextLayout);
		public abstract void Arrange(Core.DirectWrite.TextLayout baseTextLayout);
		public abstract Core.Direct2D1.RectF GetSubSyllableBounds(Core.DirectWrite.TextLayout baseTextLayout, int textPosition);
		public abstract AttachedSpecifier CreateSpecifier();

		protected Core.DirectWrite.HitTestMetrics GetMetricsForRange(Core.DirectWrite.TextLayout baseTextLayout)
		{
			if (baseTextLayout == null)
				throw new ArgumentNullException(nameof(baseTextLayout));
			if (!baseTextLayout.HitTestTextRange(Range, default, out var rangeMetrics))
				throw new InvalidOperationException("All base characters specified by single ruby group must be same script.");
			return rangeMetrics;
		}
	}

	class Ruby : Attached, IDisposable
	{
		public Ruby(Core.DirectWrite.Factory factory, Core.DirectWrite.TextRange range, string text, Core.DirectWrite.TextFormat format) : base(range)
		{
			Text = text;
			m_TextLayout = factory.CreateTextLayout(text, format, default);
		}
		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}
		protected virtual void Dispose(bool disposing)
		{
			if (!disposing) return;
			Utils.SafeDispose(ref m_TextLayout);
		}

		Vector2 m_Origin;
		Core.DirectWrite.TextLayout? m_TextLayout;

		public string Text { get; }

		public override void Recreate(Core.DirectWrite.Factory factory, Core.DirectWrite.TextFormat format)
		{
			Debug.Assert(m_TextLayout != null);
			Utils.AssignWithDispose(ref m_TextLayout, factory.CreateTextLayout(Text, format, default));
		}
		public override void Draw(Core.Direct2D1.RenderTarget renderTarget, Core.Direct2D1.Brush defaultFillBrush)
		{
			Debug.Assert(m_TextLayout != null);
			if (renderTarget == null)
				throw new ArgumentNullException(nameof(renderTarget));
			if (defaultFillBrush == null)
				throw new ArgumentNullException(nameof(defaultFillBrush));
			renderTarget.DrawTextLayout(m_Origin, m_TextLayout, defaultFillBrush);
		}
		public override float Measure(Core.DirectWrite.TextLayout baseTextLayout)
		{
			Debug.Assert(m_TextLayout != null);
			return Math.Max(m_TextLayout.Metrics.Width - GetMetricsForRange(baseTextLayout).Size.X, 0.0f) / 2;
		}
		public override void Arrange(Core.DirectWrite.TextLayout baseTextLayout)
		{
			Debug.Assert(m_TextLayout != null);
			var rangeMetrics = GetMetricsForRange(baseTextLayout);
			var nonWhitespaceClusterCount = m_TextLayout.GetClusterMetrics().Count(x => !x.IsWhitespace);
			var spacing = rangeMetrics.Size.X - m_TextLayout.Metrics.Width;
			m_TextLayout.MaxWidth = rangeMetrics.Size.X - spacing / nonWhitespaceClusterCount;
			m_TextLayout.TextAlignment = Core.DirectWrite.TextAlignment.Justified;
			var textWidth = m_TextLayout.GetClusterMetrics().Select(x => x.Width).RobustSum();
			if (textWidth < m_TextLayout.MaxWidth)
			{
				// text does not seem to be justified, so we use centering instead
				m_TextLayout.TextAlignment = Core.DirectWrite.TextAlignment.Center;
			}
			m_Origin = new Vector2(rangeMetrics.TopLeft.X + spacing / 2 / nonWhitespaceClusterCount, rangeMetrics.TopLeft.Y);
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
			Debug.Assert(m_TextLayout != null);
			var (_, metrics) = m_TextLayout.HitTestTextPosition(textPosition, false);
			var bounds = Core.Direct2D1.RectF.FromXYWH(metrics.TopLeft, metrics.Size);
			bounds.TopLeft += m_Origin;
			bounds.BottomRight += m_Origin;
			return (bounds, metrics.TextRange);
		}
	}

	class SyllableDivision : Attached
	{
		public SyllableDivision(Core.DirectWrite.TextRange range, int divisionCount) : base(range) => DivisionCount = divisionCount;

		public int DivisionCount { get; }

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

	class TextRunBase : IDisposable
	{
		protected TextRunBase(Core.DirectWrite.Factory writeFacotry, System.Windows.Media.FontFamily fontFamily, FontWeight fontWeight, FontStyle fontStyle, FontStretch fontStretch, double fontSize)
			=> Setup(writeFacotry, string.Empty, Array.Empty<AttachedSpecifier>(), fontFamily, fontWeight, fontStyle, fontStretch, fontSize);
		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}
		protected virtual void Dispose(bool disposing)
		{
			if (!disposing)
				return;
			Text = null;
			Utils.SafeDispose(ref m_TextLayout);
			CleanupAttacheds();
		}

		Core.DirectWrite.TextLayout? m_TextLayout;
		Attached[]? m_Attacheds;

		protected string? Text { get; private set; }
		protected Core.DirectWrite.TextLayout? TextLayout => m_TextLayout;
		protected IReadOnlyList<Attached>? Attacheds => m_Attacheds;

		public Vector2 Size
		{
			get
			{
				if (Text == null || m_TextLayout == null || m_Attacheds == null)
					throw new InvalidOperationException("Setup not called");
				var metrics = m_TextLayout.Metrics;
				return new Vector2(
					Math.Max(metrics.LayoutSize.X, metrics.WidthIncludingTrailingWhitespace),
					Math.Max(metrics.LayoutSize.Y, metrics.Height)
				);
			}
		}
		public void Draw(Core.Direct2D1.RenderTarget renderTarget, Core.Direct2D1.Brush defaultFillBrush)
		{
			if (m_TextLayout == null || m_Attacheds == null)
				return;
			if (renderTarget == null)
				throw new ArgumentNullException(nameof(renderTarget));
			foreach (var it in m_Attacheds)
				it.Draw(renderTarget, defaultFillBrush);
			renderTarget.DrawTextLayout(default, m_TextLayout, defaultFillBrush);
		}
		public void Setup(Core.DirectWrite.Factory writeFactory, string text, IReadOnlyList<AttachedSpecifier> attachedSpecifiers, System.Windows.Media.FontFamily fontFamily, FontWeight fontWeight, FontStyle fontStyle, FontStretch fontStretch, double fontSize)
		{
			if (attachedSpecifiers == null)
				throw new ArgumentNullException(nameof(attachedSpecifiers));
			var fontFamilyName = fontFamily.Source;
			var dwriteFontWeight = fontWeight.ToOpenTypeWeight();
			var dwriteFontStyle = fontStyle.ToDWrite();
			var dwriteFontStretch = fontStretch.ToOpenTypeStretch();
			Text = text;
			using (var format = writeFactory.CreateTextFormat(fontFamilyName, dwriteFontWeight, dwriteFontStyle, dwriteFontStretch, (float)fontSize))
			{
				format.WordWrapping = Core.DirectWrite.WordWrapping.NoWrap;
				format.LineSpacing = (Core.DirectWrite.LineSpacingMethod.Uniform, (float)fontSize * 1.5f * (1.0f / 0.8f), (float)fontSize * 1.5f);
				Utils.AssignWithDispose(ref m_TextLayout, writeFactory.CreateTextLayout(Text, format, default));
				Debug.Assert(m_TextLayout != null);
			}
			CleanupAttacheds();
			m_Attacheds = new Attached[attachedSpecifiers.Count];
			using (var format = writeFactory.CreateTextFormat(fontFamilyName, dwriteFontWeight, dwriteFontStyle, dwriteFontStretch, (float)fontSize / 2))
			{
				format.WordWrapping = Core.DirectWrite.WordWrapping.NoWrap;
				for (var i = 0; i < attachedSpecifiers.Count; i++)
				{
					m_Attacheds[i] = attachedSpecifiers[i] switch
					{
						RubySpecifier rubySpecifier => new Ruby(writeFactory, rubySpecifier.Range, rubySpecifier.Text, format),
						SyllableDivisionSpecifier syllableDivisionSpecifier => new SyllableDivision(syllableDivisionSpecifier.Range, syllableDivisionSpecifier.DivisionCount),
						_ => throw new NotSupportedException($"Unsupported attached specifier: {attachedSpecifiers[i]?.GetType()}"),
					};
					var spacing = m_Attacheds[i].Measure(m_TextLayout);
					SetRangeSpacing(spacing, spacing, 0, m_Attacheds[i].Range);
					m_Attacheds[i].Arrange(m_TextLayout);
				}
			}
		}
		public void ChangeFont(Core.DirectWrite.Factory writeFactory, System.Windows.Media.FontFamily fontFamily, FontWeight fontWeight, FontStyle fontStyle, FontStretch fontStretch, double fontSize)
		{
			if (Text == null || m_TextLayout == null || m_Attacheds == null)
				throw new InvalidOperationException("Setup not called");
			var fontFamilyName = fontFamily.Source;
			var dwriteFontWeight = fontWeight.ToOpenTypeWeight();
			var dwriteFontStyle = fontStyle.ToDWrite();
			var dwriteFontStretch = fontStretch.ToOpenTypeStretch();
			using (var format = writeFactory.CreateTextFormat(fontFamilyName, dwriteFontWeight, dwriteFontStyle, dwriteFontStretch, (float)fontSize))
			{
				format.WordWrapping = Core.DirectWrite.WordWrapping.NoWrap;
				format.LineSpacing = (Core.DirectWrite.LineSpacingMethod.Uniform, (float)fontSize * 1.5f * (1.0f / 0.8f), (float)fontSize * 1.5f);
				Utils.AssignWithDispose(ref m_TextLayout, writeFactory.CreateTextLayout(Text, format, default));
			}
			using (var format = writeFactory.CreateTextFormat(fontFamilyName, dwriteFontWeight, dwriteFontStyle, dwriteFontStretch, (float)fontSize / 2))
			{
				format.WordWrapping = Core.DirectWrite.WordWrapping.NoWrap;
				foreach (var attached in m_Attacheds)
				{
					attached.Recreate(writeFactory, format);
					var spacing = attached.Measure(m_TextLayout);
					SetRangeSpacing(spacing, spacing, 0, attached.Range);
					attached.Arrange(m_TextLayout);
				}
			}
		}

		void CleanupAttacheds()
		{
			if (m_Attacheds != null)
			{
				foreach (var item in m_Attacheds)
				{
					if (item is IDisposable disposable)
						disposable.Dispose();
				}
				m_Attacheds = null;
			}
		}
		void SetRangeSpacing(float leadingSpacing, float trailingSpacing, float minimumAdvanceWidth, Core.DirectWrite.TextRange range)
		{
			Debug.Assert(m_TextLayout != null);
			var metricsForRange = new List<(Core.DirectWrite.TextRange Range, bool IsRightToLeft)>();
			var clusters = m_TextLayout.GetClusterMetrics();
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
			using var textLayout1 = Core.DirectWrite.TextLayout1.From(m_TextLayout);
			if (metricsForRange.Count == 1)
				textLayout1.SetCharacterSpacing(leadingSpacing, trailingSpacing, minimumAdvanceWidth, range);
			else
			{
				static void SwapSpacing(ref float l, ref float t)
				{
					var tmp = t;
					t = l;
					l = tmp;
				}

				var (leading, trailing) = (leadingSpacing, 0.0f);
				if (metricsForRange[0].IsRightToLeft)
					SwapSpacing(ref leading, ref trailing);
				textLayout1.SetCharacterSpacing(leading, trailing, minimumAdvanceWidth, metricsForRange[0].Range);

				(leading, trailing) = (0.0f, trailingSpacing);
				if (metricsForRange.Last().IsRightToLeft)
					SwapSpacing(ref leading, ref trailing);
				textLayout1.SetCharacterSpacing(leading, trailing, minimumAdvanceWidth, metricsForRange.Last().Range);
			}
		}
	}

	class TextRun : TextRunBase
	{
		public TextRun(Core.DirectWrite.Factory writeFacotry, System.Windows.Media.FontFamily fontFamily, FontWeight fontWeight, FontStyle fontStyle, FontStretch fontStretch, double fontSize)
			: base(writeFacotry, fontFamily, fontWeight, fontStyle, fontStretch, fontSize) { }

		public (Core.DirectWrite.LineSpacingMethod LineSpacingMethod, float LineSpacing, float Baseline) LineSpacing
		{
			get
			{
				if (Text == null || TextLayout == null || Attacheds == null)
					throw new InvalidOperationException("Setup not called");
				return TextLayout.LineSpacing;
			}
		}

		public Core.Direct2D1.RectF GetSubSyllableBounds(SubSyllable subSyllable)
		{
			if (Text == null || TextLayout == null || Attacheds == null)
				throw new InvalidOperationException("Setup not called");
			if (subSyllable.IsSimple)
			{
				// HitTestTextPoint retrieves actual character bounds, while HitTestTextRange retrieves line-based bounds.
				var (_, metrics) = TextLayout.HitTestTextPosition(subSyllable.CharacterIndex, false);
				if (!TextLayout.HitTestTextRange(metrics.TextRange, default, out var rangeMetrics))
					throw new InvalidOperationException("One index must reference one script group.");
				return Core.Direct2D1.RectF.FromLTRB(new Vector2(rangeMetrics.TopLeft.X, metrics.TopLeft.Y), rangeMetrics.BottomRight);
			}
			else
				return Attacheds[subSyllable.AttachedIndex].GetSubSyllableBounds(TextLayout, subSyllable.CharacterIndex);
		}
		public (string Text, AttachedSpecifier[] AttachedSpecifiers) GetLine(int lineIndex, LineMap lineMap)
		{
			if (Text == null || TextLayout == null || Attacheds == null)
				throw new InvalidOperationException("Setup not called");
			var physicalLine = lineMap.GetPhysicalLineByLogical(lineIndex);
			var text = Text.Substring(physicalLine.TextStart, physicalLine.TextLength).TrimEnd('\n');
			var attachedSpecs = Attacheds.Skip(physicalLine.AttachedStart).Take(physicalLine.AttachedLength).Select(x => x.CreateSpecifier().Move(-physicalLine.TextStart)).ToArray();
			return (text, attachedSpecs);
		}
	}

	class NextTextRun : TextRunBase
	{
		public NextTextRun(Core.DirectWrite.Factory writeFacotry, System.Windows.Media.FontFamily fontFamily, FontWeight fontWeight, FontStyle fontStyle, FontStretch fontStretch, double fontSize)
			: base(writeFacotry, fontFamily, fontWeight, fontStyle, fontStretch, fontSize) { }
	}

	static class DWriteExtensions
	{
		public static Core.DirectWrite.FontStyle ToDWrite(this System.Windows.FontStyle fontStyle) =>
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
}
