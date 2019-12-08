using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Lyriser.Models;
using SharpDX;
using SharpDX.Direct2D1;
using SharpDX.DirectWrite;

using D2D1Factory = SharpDX.Direct2D1.Factory;
using DWriteFactory = SharpDX.DirectWrite.Factory;

namespace Lyriser.Views
{
	public class LyricsViewer : D2dControl.D2dControl
	{
		public LyricsViewer()
		{
			resCache.Add("TextBrush", rt => new SolidColorBrush(rt, Color.Black));
			resCache.Add("NextTextBrush", rt => new SolidColorBrush(rt, Color.White));
			resCache.Add("HighlightBrush", rt => new SolidColorBrush(rt, Color.Cyan));
			Loaded += OnLoaded;
			Unloaded += OnUnloaded;
			Focusable = true;
		}

		System.Windows.Media.FontFamily FontFamily { get; } = new System.Windows.Media.FontFamily("Meiryo");
		System.Windows.FontWeight FontWeight => FontWeights.Normal;
		System.Windows.FontStyle FontStyle => FontStyles.Normal;
		System.Windows.FontStretch FontStretch => FontStretches.Normal;
		double FontSize => 14.0 * 96.0 / 72.0;

		const float s_LeftPadding = 10;
		const float s_TopPadding = 5;
		const float s_RightPadding = 10;
		const float s_BottomPadding = 5;
		const float s_NextTopPadding = 5;
		const float s_NextBottomPadding = 5;

		D2D1Factory m_Factory;
		DWriteFactory m_WriteFactory;
		TextRun m_Run;
		NextTextRun m_NextRun;

		public static readonly new DependencyProperty SourceProperty = DependencyProperty.Register(nameof(Source), typeof(LyricsSource), typeof(LyricsViewer), new PropertyMetadata(LyricsSource.Empty, (s, e) => ((LyricsViewer)s).OnSourceChanged(e)));
		public static readonly DependencyProperty CurrentSyllableProperty = DependencyProperty.Register(
			nameof(CurrentSyllable), typeof(SyllableLocation), typeof(LyricsViewer),
			new FrameworkPropertyMetadata(default(SyllableLocation), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (s, e) => ((LyricsViewer)s).UpdateNextLineViewer()));
		public static readonly DependencyProperty ScrollPositionXProperty = DependencyProperty.Register(nameof(ScrollPositionX), typeof(double), typeof(LyricsViewer), new PropertyMetadata(0.0, null, (s, v) => Utils.Clamp((double)v, 0, ((LyricsViewer)s).ScrollMaximumX)));
		public static readonly DependencyProperty ScrollPositionYProperty = DependencyProperty.Register(nameof(ScrollPositionY), typeof(double), typeof(LyricsViewer), new PropertyMetadata(0.0, null, (s, v) => Utils.Clamp((double)v, 0, ((LyricsViewer)s).ScrollMaximumY)));
		static readonly DependencyPropertyKey ScrollMaximumXPropertyKey = DependencyProperty.RegisterReadOnly(nameof(ScrollMaximumX), typeof(double), typeof(LyricsViewer), new PropertyMetadata());
		static readonly DependencyPropertyKey ScrollMaximumYPropertyKey = DependencyProperty.RegisterReadOnly(nameof(ScrollMaximumY), typeof(double), typeof(LyricsViewer), new PropertyMetadata());
		public static readonly DependencyProperty ScrollMaximumXProperty = ScrollMaximumXPropertyKey.DependencyProperty;
		public static readonly DependencyProperty ScrollMaximumYProperty = ScrollMaximumYPropertyKey.DependencyProperty;

		void OnLoaded(object sender, RoutedEventArgs e)
		{
			m_Factory = new D2D1Factory();
			m_WriteFactory = new DWriteFactory();
			m_Run = new TextRun(m_WriteFactory, FontFamily, FontWeight, FontStyle, FontStretch, FontSize);
			m_NextRun = new NextTextRun(m_WriteFactory, FontFamily, FontWeight, FontStyle, FontStretch, FontSize);
		}
		void OnUnloaded(object sender, RoutedEventArgs e)
		{
			Utils.SafeDispose(ref m_Run);
			Utils.SafeDispose(ref m_NextRun);
			Utils.SafeDispose(ref m_WriteFactory);
			Utils.SafeDispose(ref m_Factory);
		}

		protected virtual void OnSourceChanged(DependencyPropertyChangedEventArgs e)
		{
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
			var subSyllables = Source.SyllableLines[CurrentSyllable.Line][CurrentSyllable.Column];
			var centerX = (m_Run.GetSubSyllableBounds(subSyllables[0]).Left + m_Run.GetSubSyllableBounds(subSyllables.Last()).Right) / 2.0f;
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
			CurrentSyllable = new SyllableLocation(Source.SyllableLines.Count - 1, Source.SyllableLines.Last().Length - 1);
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
			var distance = float.PositiveInfinity;
			var candidateLineIndex = -1;
			for (var i = 0; i < Source.SyllableLines.Count; i++)
			{
				var bounds = m_Run.GetSubSyllableBounds(Source.SyllableLines[i][0][0]);
				var dist = Math.Abs(y + m_Run.LineSpacing.Height / 2 - bounds.Bottom);
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
			var distance = float.PositiveInfinity;
			var nearestIndex = 0;
			for (var i = 0; i < Source.SyllableLines[lineIndex].Length; i++)
			{
				foreach (var subSyllable in Source.SyllableLines[lineIndex][i])
				{
					var bounds = m_Run.GetSubSyllableBounds(subSyllable);
					var dist = Math.Abs(x - (bounds.Left + bounds.Right) / 2);
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
			var rects = syllable.Select(x => m_Run.GetSubSyllableBounds(x)).ToArray();
			if (rects.Length <= 0)
				throw new ArgumentException("Syllable must contain at least one sub-syllable", nameof(syllable));
			ScrollInto(new RectangleF()
			{
				Left = rects.Min(x => x.Left) - s_LeftPadding,
				Top = rects[0].Bottom - m_Run.LineSpacing.Height - s_TopPadding,
				Right = rects.Max(x => x.Right) + s_RightPadding,
				Bottom = rects[0].Bottom + s_BottomPadding
			});
		}
		void ScrollInto(RectangleF bounds)
		{
			var transform = ViewTransform;
			transform.Invert();
			var topLeft = Matrix3x2.TransformPoint(transform, new Vector2());
			var bottomRight = Matrix3x2.TransformPoint(transform, new Vector2((float)ActualWidth, (float)ActualHeight - NextLineViewerHeight));

			float offsetX = 0;
			if (bounds.Left < topLeft.X)
				offsetX += bounds.Left - topLeft.X;
			else if (bounds.Right > bottomRight.X)
				offsetX += bounds.Right - bottomRight.X;
			float offsetY = 0;
			if (bounds.Top < topLeft.Y)
				offsetY += bounds.Top - topLeft.Y;
			else if (bounds.Bottom > bottomRight.Y)
				offsetY += bounds.Bottom - bottomRight.Y;

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
		Matrix3x2 ViewTransform => Matrix3x2.Translation(-(float)ScrollPositionX + s_LeftPadding, -(float)ScrollPositionY + s_TopPadding);
		float NextLineViewerHeight => s_NextTopPadding + m_NextRun.Size.Height + s_NextBottomPadding;

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
		public override void Render(RenderTarget target)
		{
			if (IsInDesignMode)
				return;
			target.Clear(Color.White);
			var transform = ViewTransform;
			target.Transform = transform;
			if (Source != null && Source.SyllableLines.Count > 0)
			{
				foreach (var subSyllable in Source.SyllableLines[CurrentSyllable.Line][CurrentSyllable.Column])
					target.FillRectangle(m_Run.GetSubSyllableBounds(subSyllable), (Brush)resCache["HighlightBrush"]);
			}
			m_Run.Draw(target, (Brush)resCache["TextBrush"]);
			target.Transform = Matrix3x2.Identity;

			var size = target.Size;
			target.PushAxisAlignedClip(new RectangleF(0, size.Height - NextLineViewerHeight, size.Width, NextLineViewerHeight), AntialiasMode.Aliased);
			target.Clear(Color.Gray);
			target.Transform = Matrix3x2.Translation(transform.M31, size.Height - NextLineViewerHeight + s_NextTopPadding);
			m_NextRun.Draw(target, (Brush)resCache["NextTextBrush"]);
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
			var transform = ViewTransform;
			transform.Invert();
			var pt = e.GetPosition(this);
			CurrentSyllable = HitTestPoint(Matrix3x2.TransformPoint(transform, new Vector2((float)pt.X, (float)pt.Y)));
		}
		protected override void OnMouseWheel(MouseWheelEventArgs e)
		{
			base.OnMouseWheel(e);
			var offset = -e.Delta / Mouse.MouseWheelDeltaForOneLine * m_Run.LineSpacing.Height;
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
			else if (e.Key == Key.Up || e.Key == Key.Down)
				HighlightNextLine(e.Key == Key.Down);
			e.Handled = true;
		}

		void UpdateScrollInfo()
		{
			var size = m_Run.Size;
			// Add paddings
			size.Width += s_LeftPadding + s_RightPadding;
			size.Height += s_TopPadding + s_BottomPadding;
			// Add next line viewer height
			size.Height += NextLineViewerHeight;
			ScrollMaximumX = Math.Max(size.Width - ActualWidth, 0.0);
			ScrollMaximumY = Math.Max(size.Height - ActualHeight, 0.0);
		}
	}

	abstract class Attached
	{
		protected Attached(TextRange range) => Range = range;

		public TextRange Range { get; }

		public abstract void Recreate(DWriteFactory factory, TextFormat format);
		public abstract void Draw(RenderTarget renderTarget, Brush defaultFillBrush);
		public abstract float Measure(TextLayout baseTextLayout);
		public abstract void Arrange(TextLayout baseTextLayout);
		public abstract RectangleF GetSubSyllableBounds(TextLayout baseTextLayout, int textPosition);
		public abstract AttachedSpecifier CreateSpecifier();

		protected HitTestMetrics GetMetricsForRange(TextLayout baseTextLayout)
		{
			if (baseTextLayout == null)
				throw new ArgumentNullException(nameof(baseTextLayout));
			var ranges = baseTextLayout.HitTestTextRange(Range.StartPosition, Range.Length, 0, 0);
			if (ranges.Length > 1)
				throw new InvalidOperationException("All base characters specified by single ruby group must be same script.");
			return ranges[0];
		}
	}

	class Ruby : Attached, IDisposable
	{
		public Ruby(DWriteFactory factory, TextRange range, string text, TextFormat format) : base(range)
		{
			Text = text;
			m_TextLayout = new TextLayout(factory, text, format, 0f, 0f);
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
		TextLayout m_TextLayout;

		public string Text { get; }

		public override void Recreate(DWriteFactory factory, TextFormat format) => Utils.AssignWithDispose(ref m_TextLayout, new TextLayout(factory, Text, format, 0f, 0f));
		public override void Draw(RenderTarget renderTarget, Brush defaultFillBrush)
		{
			if (renderTarget == null)
				throw new ArgumentNullException(nameof(renderTarget));
			if (defaultFillBrush == null)
				throw new ArgumentNullException(nameof(defaultFillBrush));
			renderTarget.DrawTextLayout(m_Origin, m_TextLayout, defaultFillBrush);
		}
		public override float Measure(TextLayout baseTextLayout) => Math.Max(m_TextLayout.Metrics.Width - GetMetricsForRange(baseTextLayout).Width, 0.0f) / 2;
		public override void Arrange(TextLayout baseTextLayout)
		{
			var rangeMetrics = GetMetricsForRange(baseTextLayout);
			var nonWhitespaceClusterCount = m_TextLayout.GetClusterMetrics().Count(x => !x.IsWhitespace);
			var spacing = rangeMetrics.Width - m_TextLayout.Metrics.Width;
			m_TextLayout.MaxWidth = rangeMetrics.Width - spacing / nonWhitespaceClusterCount;
			m_TextLayout.TextAlignment = SharpDX.DirectWrite.TextAlignment.Justified;
			var textWidth = m_TextLayout.GetClusterMetrics().Select(x => x.Width).RobustSum();
			if (textWidth < m_TextLayout.MaxWidth)
			{
				// text does not seem to be justified, so we use centering instead
				m_TextLayout.TextAlignment = SharpDX.DirectWrite.TextAlignment.Center;
			}
			m_Origin = new Vector2(rangeMetrics.Left + spacing / 2 / nonWhitespaceClusterCount, rangeMetrics.Top);
		}
		public override RectangleF GetSubSyllableBounds(TextLayout baseTextLayout, int textPosition)
		{
			var metrics = GetMetricsForRange(baseTextLayout);
			var (bounds, range) = GetCharacterBounds(textPosition);
			return new RectangleF()
			{
				Left = range.StartPosition > 0 ? bounds.Left : metrics.Left,
				Top = bounds.Top,
				Right = range.StartPosition + range.Length < Text.Length ? bounds.Right : metrics.Left + metrics.Width,
				Bottom = metrics.Top + metrics.Height
			};
		}
		public override AttachedSpecifier CreateSpecifier() => new RubySpecifier(Range, Text);

		(RectangleF Value, TextRange Range) GetCharacterBounds(int textPosition)
		{
			var metrics = m_TextLayout.HitTestTextPosition(textPosition, false, out var _, out var _);
			var bounds = new RectangleF(metrics.Left, metrics.Top, metrics.Width, metrics.Height);
			bounds.Offset(m_Origin);
			return (bounds, new TextRange(metrics.TextPosition, metrics.Length));
		}
	}

	class SyllableDivision : Attached
	{
		public SyllableDivision(TextRange range, int divisionCount) : base(range) => DivisionCount = divisionCount;

		public int DivisionCount { get; }

		public override void Recreate(DWriteFactory factory, TextFormat format) { }
		public override void Draw(RenderTarget renderTarget, Brush defaultFillBrush) { }
		public override float Measure(TextLayout baseTextLayout) => 0.0f;
		public override void Arrange(TextLayout baseTextLayout) { }
		public override RectangleF GetSubSyllableBounds(TextLayout baseTextLayout, int textPosition)
		{
			var rangeMetrics = GetMetricsForRange(baseTextLayout);
			var metrics = baseTextLayout.HitTestTextPosition(Range.StartPosition, false, out var _, out var _);
			return new RectangleF(rangeMetrics.Left + rangeMetrics.Width * textPosition / DivisionCount, metrics.Top, rangeMetrics.Width / DivisionCount, rangeMetrics.Top + rangeMetrics.Height - metrics.Top);
		}
		public override AttachedSpecifier CreateSpecifier() => new SyllableDivisionSpecifier(Range, DivisionCount);
	}

	class TextRunBase : IDisposable
	{
		protected TextRunBase(DWriteFactory writeFacotry, System.Windows.Media.FontFamily fontFamily, System.Windows.FontWeight fontWeight, System.Windows.FontStyle fontStyle, System.Windows.FontStretch fontStretch, double fontSize)
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

		TextLayout m_TextLayout;
		Attached[] m_Attacheds;

		protected string Text { get; private set; }
		protected TextLayout TextLayout => m_TextLayout;
		protected IReadOnlyList<Attached> Attacheds => m_Attacheds;

		public Size2F Size
		{
			get
			{
				var metrics = m_TextLayout.Metrics;
				return new Size2F(
					Math.Max(metrics.LayoutWidth, metrics.WidthIncludingTrailingWhitespace),
					Math.Max(metrics.LayoutHeight, metrics.Height)
				);
			}
		}
		public void Draw(RenderTarget renderTarget, Brush defaultFillBrush)
		{
			if (m_TextLayout == null)
				return;
			if (renderTarget == null)
				throw new ArgumentNullException(nameof(renderTarget));
			foreach (var it in m_Attacheds)
				it.Draw(renderTarget, defaultFillBrush);
			renderTarget.DrawTextLayout(default, m_TextLayout, defaultFillBrush);
		}
		public void Setup(DWriteFactory writeFactory, string text, IReadOnlyList<AttachedSpecifier> attachedSpecifiers, System.Windows.Media.FontFamily fontFamily, System.Windows.FontWeight fontWeight, System.Windows.FontStyle fontStyle, System.Windows.FontStretch fontStretch, double fontSize)
		{
			if (attachedSpecifiers == null)
				throw new ArgumentNullException(nameof(attachedSpecifiers));
			var fontFamilyName = fontFamily.Source;
			var dwriteFontWeight = fontWeight.ToDWrite();
			var dwriteFontStyle = fontStyle.ToDWrite();
			var dwriteFontStretch = fontStretch.ToDWrite();
			Text = text;
			using (var format = new TextFormat(writeFactory, fontFamilyName, dwriteFontWeight, dwriteFontStyle, dwriteFontStretch, (float)fontSize))
			{
				format.WordWrapping = WordWrapping.NoWrap;
				format.SetLineSpacing(LineSpacingMethod.Uniform, (float)fontSize * 1.5f * (1.0f / 0.8f), (float)fontSize * 1.5f);
				Utils.AssignWithDispose(ref m_TextLayout, new TextLayout(writeFactory, Text, format, 0f, 0f));
			}
			CleanupAttacheds();
			m_Attacheds = new Attached[attachedSpecifiers.Count];
			using (var format = new TextFormat(writeFactory, fontFamilyName, dwriteFontWeight, dwriteFontStyle, dwriteFontStretch, (float)fontSize / 2))
			{
				format.WordWrapping = WordWrapping.NoWrap;
				for (var i = 0; i < attachedSpecifiers.Count; i++)
				{
					switch (attachedSpecifiers[i])
					{
						case RubySpecifier rubySpecifier:
							m_Attacheds[i] = new Ruby(writeFactory, rubySpecifier.Range, rubySpecifier.Text, format);
							break;
						case SyllableDivisionSpecifier syllableDivisionSpecifier:
							m_Attacheds[i] = new SyllableDivision(syllableDivisionSpecifier.Range, syllableDivisionSpecifier.DivisionCount);
							break;
						default:
							throw new NotSupportedException($"Unsupported attached specifier: {attachedSpecifiers[i]?.GetType()}");
					}
					var spacing = m_Attacheds[i].Measure(m_TextLayout);
					SetRangeSpacing(spacing, spacing, 0, m_Attacheds[i].Range);
					m_Attacheds[i].Arrange(m_TextLayout);
				}
			}
		}
		public void ChangeFont(DWriteFactory writeFactory, System.Windows.Media.FontFamily fontFamily, System.Windows.FontWeight fontWeight, System.Windows.FontStyle fontStyle, System.Windows.FontStretch fontStretch, double fontSize)
		{
			var fontFamilyName = fontFamily.Source;
			var dwriteFontWeight = fontWeight.ToDWrite();
			var dwriteFontStyle = fontStyle.ToDWrite();
			var dwriteFontStretch = fontStretch.ToDWrite();
			using (var format = new TextFormat(writeFactory, fontFamilyName, dwriteFontWeight, dwriteFontStyle, dwriteFontStretch, (float)fontSize))
			{
				format.WordWrapping = WordWrapping.NoWrap;
				format.SetLineSpacing(LineSpacingMethod.Uniform, (float)fontSize * 1.5f * (1.0f / 0.8f), (float)fontSize * 1.5f);
				Utils.AssignWithDispose(ref m_TextLayout, new TextLayout(writeFactory, Text, format, 0f, 0f));
			}
			using (var format = new TextFormat(writeFactory, fontFamilyName, dwriteFontWeight, dwriteFontStyle, dwriteFontStretch, (float)fontSize / 2))
			{
				format.WordWrapping = WordWrapping.NoWrap;
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
		void SetRangeSpacing(float leadingSpacing, float trailingSpacing, float minimumAdvanceWidth, TextRange range)
		{
			var metricsForRange = new List<(TextRange Range, bool IsRightToLeft)>();
			var clusters = m_TextLayout.GetClusterMetrics();
			var start = 0;
			foreach (var cluster in clusters)
			{
				var newRangeStart = Math.Max(start, range.StartPosition);
				var newRangeEnd = Math.Min(start + cluster.Length, range.StartPosition + range.Length);
				if (newRangeEnd > newRangeStart)
				{
					metricsForRange.Add((new TextRange(newRangeStart, newRangeEnd - newRangeStart), cluster.IsRightToLeft));
					if (start + cluster.Length >= range.StartPosition + range.Length) break;
				}
				start += cluster.Length;
			}
			using (var textLayout1 = m_TextLayout.QueryInterface<TextLayout1>())
			{
				if (metricsForRange.Count == 1)
					textLayout1.SetCharacterSpacing(leadingSpacing, trailingSpacing, minimumAdvanceWidth, range);
				else
				{
					void SwapSpacing(ref float l, ref float t)
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
	}

	class TextRun : TextRunBase
	{
		public TextRun(DWriteFactory writeFacotry, System.Windows.Media.FontFamily fontFamily, System.Windows.FontWeight fontWeight, System.Windows.FontStyle fontStyle, System.Windows.FontStretch fontStretch, double fontSize)
			: base(writeFacotry, fontFamily, fontWeight, fontStyle, fontStretch, fontSize) { }

		public LineSpacing LineSpacing
		{
			get
			{
				TextLayout.GetLineSpacing(out var method, out var lineSpacing, out var baseline);
				return new LineSpacing() { Method = method, Height = lineSpacing, Baseline = baseline };
			}
		}
		public RectangleF GetSubSyllableBounds(SubSyllable subSyllable)
		{
			if (subSyllable.IsSimple)
			{
				// HitTestTextPoint retrieves actual character bounds, while HitTestTextRange retrieves line-based bounds.
				var metrics = TextLayout.HitTestTextPosition(subSyllable.CharacterIndex, false, out var _, out var _);
				var rangeMetrics = TextLayout.HitTestTextRange(metrics.TextPosition, metrics.Length, 0f, 0f);
				if (rangeMetrics.Length > 1)
					throw new InvalidOperationException("One index must reference one script group.");
				return new RectangleF(rangeMetrics[0].Left, metrics.Top, rangeMetrics[0].Width, rangeMetrics[0].Top + rangeMetrics[0].Height - metrics.Top);
			}
			else
				return Attacheds[subSyllable.AttachedIndex].GetSubSyllableBounds(TextLayout, subSyllable.CharacterIndex);
		}
		public (string Text, AttachedSpecifier[] AttachedSpecifiers) GetLine(int lineIndex, LineMap lineMap)
		{
			var physicalLine = lineMap.GetPhysicalLineByLogical(lineIndex);
			var text = Text.Substring(physicalLine.TextStart, physicalLine.TextLength).TrimEnd('\n');
			var attachedSpecs = Attacheds.Skip(physicalLine.AttachedStart).Take(physicalLine.AttachedLength).Select(x => x.CreateSpecifier().Move(-physicalLine.TextStart)).ToArray();
			return (text, attachedSpecs);
		}
	}

	class NextTextRun : TextRunBase
	{
		public NextTextRun(DWriteFactory writeFacotry, System.Windows.Media.FontFamily fontFamily, System.Windows.FontWeight fontWeight, System.Windows.FontStyle fontStyle, System.Windows.FontStretch fontStretch, double fontSize)
			: base(writeFacotry, fontFamily, fontWeight, fontStyle, fontStretch, fontSize) { }
	}

	static class DWriteExtensions
	{
		public static SharpDX.DirectWrite.FontWeight ToDWrite(this System.Windows.FontWeight fontWeight) => (SharpDX.DirectWrite.FontWeight)fontWeight.ToOpenTypeWeight();
		public static SharpDX.DirectWrite.FontStyle ToDWrite(this System.Windows.FontStyle fontStyle) =>
			  fontStyle == FontStyles.Italic ? SharpDX.DirectWrite.FontStyle.Italic
			: fontStyle == FontStyles.Oblique ? SharpDX.DirectWrite.FontStyle.Oblique
			: SharpDX.DirectWrite.FontStyle.Normal;
		public static SharpDX.DirectWrite.FontStretch ToDWrite(this System.Windows.FontStretch fontStretch) => (SharpDX.DirectWrite.FontStretch)fontStretch.ToOpenTypeStretch();
	}

	static class FloatUtils
	{
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
