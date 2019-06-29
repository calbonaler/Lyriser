using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using SharpDX;
using SharpDX.Direct2D1;
using SharpDX.DirectWrite;

using D2D1Factory = SharpDX.Direct2D1.Factory;
using DWriteFactory = SharpDX.DirectWrite.Factory;

namespace Lyriser
{
	public class LyricsViewer : Control
	{
		public LyricsViewer()
		{
			m_Factory = new D2D1Factory();
			m_WriteFactory = new DWriteFactory();
			m_Run = new TextRun(m_WriteFactory, Font);
			m_NextRun = new TextRun(m_WriteFactory, Font);
			m_KeyLines = new CharacterIndex[0][][];
		}
		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);
			if (!disposing) return;
			Utils.SafeDispose(ref m_TextBrush);
			Utils.SafeDispose(ref m_NextTextBrush);
			Utils.SafeDispose(ref m_HighlightBrush);
			Utils.SafeDispose(ref m_Run);
			Utils.SafeDispose(ref m_NextRun);
			Utils.SafeDispose(ref m_RenderTarget);
			Utils.SafeDispose(ref m_WriteFactory);
			Utils.SafeDispose(ref m_Factory);
		}

		const float s_LeftPadding = 10;
		const float s_TopPadding = 5;
		const float s_RightPadding = 10;
		const float s_BottomPadding = 5;
		const float s_NextTopPadding = 5;
		const float s_NextBottomPadding = 5;

		D2D1Factory m_Factory;
		DWriteFactory m_WriteFactory;
		WindowRenderTarget m_RenderTarget;
		SolidColorBrush m_TextBrush;
		SolidColorBrush m_NextTextBrush;
		SolidColorBrush m_HighlightBrush;
		TextRun m_Run;
		TextRun m_NextRun;
		CharacterIndex[][][] m_KeyLines;
		(int Line, int Column) m_Index;

		int DipToPixel(float dip) => (int)(dip * DeviceDpi / 96f);
		float PixelToDip(int pixel) => pixel * 96f / DeviceDpi;

		public void Setup(IEnumerable<(AttachedLine Line, CharacterIndex[][] Keys)> lines)
		{
			if (lines == null)
				throw new ArgumentNullException(nameof(lines));
			var keyLines = new List<CharacterIndex[][]>();
			var attachedLines = new List<AttachedLine>();
			foreach (var (line, keys) in lines)
			{
				if (keys.Length > 0)
					keyLines.Add(keys);
				attachedLines.Add(line);
			}
			m_KeyLines = keyLines.ToArray();
			m_Run.Setup(m_WriteFactory, attachedLines, Font);
			UpdateNextLineViewer();
			UpdateScrollInfo();
			HighlightFirst();
		}
		public void HighlightNext(bool forward)
		{
			var newLineIndex = Index.Line;
			var newColumnIndex = Index.Column + (forward ? 1 : -1);
			if (newColumnIndex >= 0 && newColumnIndex < m_KeyLines[newLineIndex].Length)
				Index = (newLineIndex, newColumnIndex);
			else
			{
				newLineIndex += forward ? 1 : -1;
				if (newLineIndex >= 0 && newLineIndex < m_KeyLines.Length)
				{
					newColumnIndex = forward ? 0 : m_KeyLines[newLineIndex].Length - 1;
					Index = (newLineIndex, newColumnIndex);
				}
			}
			Invalidate();
			ScrollInto(Index);
		}
		public void HighlightNextLine(bool forward)
		{
			var subKeys = m_KeyLines[Index.Line][Index.Column];
			var centerX = (m_Run.GetCharacterBounds(subKeys[0]).Left + m_Run.GetCharacterBounds(subKeys.Last()).Right) / 2.0f;
			var newLineIndex = Index.Line + (forward ? 1 : -1);
			if (newLineIndex >= 0 && newLineIndex < m_KeyLines.Length)
				Index = (newLineIndex, FindNearestSyllableIndex(newLineIndex, centerX));
			Invalidate();
			ScrollInto(Index);
		}
		public void HighlightFirst()
		{
			Index = (0, 0);
			Invalidate();
			ScrollPositionX = this.GetScrollInfo(ScrollBarKind.Horizontal, ScrollInfoMasks.Range).Minimum;
			ScrollPositionY = this.GetScrollInfo(ScrollBarKind.Vertical, ScrollInfoMasks.Range).Minimum;
		}
		public void HighlightLast()
		{
			Index = (m_KeyLines.Length - 1, m_KeyLines.Last().Length - 1);
			Invalidate();
			ScrollPositionX = this.GetScrollInfo(ScrollBarKind.Horizontal, ScrollInfoMasks.Range).Maximum;
			ScrollPositionY = this.GetScrollInfo(ScrollBarKind.Vertical, ScrollInfoMasks.Range).Maximum;
		}
		void UpdateNextLineViewer()
		{
			if (Index.Line + 1 < m_KeyLines.Length)
				m_NextRun.Setup(m_WriteFactory, new[] { m_Run.GetLine(m_KeyLines[Index.Line + 1][0][0].Line) }, Font);
			else
				m_NextRun.Setup(m_WriteFactory, new AttachedLine[0], Font);
		}
		void CreateDeviceDependentResources()
		{
			Utils.AssignWithDispose(ref m_RenderTarget, new WindowRenderTarget(m_Factory, default, new HwndRenderTargetProperties() { Hwnd = Handle, PixelSize = new Size2(ClientSize.Width, ClientSize.Height) }));
			m_RenderTarget.DotsPerInch = new Size2F(DeviceDpi, DeviceDpi);
			Utils.AssignWithDispose(ref m_TextBrush, new SolidColorBrush(m_RenderTarget, ForeColor.ToColor4()));
			Utils.AssignWithDispose(ref m_NextTextBrush, new SolidColorBrush(m_RenderTarget, Color.White));
			Utils.AssignWithDispose(ref m_HighlightBrush, new SolidColorBrush(m_RenderTarget, System.Drawing.Color.Cyan.ToColor4()));
		}
		void DiscardDeviceDependentResources()
		{
			Utils.SafeDispose(ref m_TextBrush);
			Utils.SafeDispose(ref m_NextTextBrush);
			Utils.SafeDispose(ref m_HighlightBrush);
			Utils.SafeDispose(ref m_RenderTarget);
		}
		void BeginDraw()
		{
			if (m_RenderTarget == null) CreateDeviceDependentResources();
			m_RenderTarget.BeginDraw();
		}
		void EndDraw()
		{
			var hr = m_RenderTarget.TryEndDraw(out var _, out var _);
			if (hr == ResultCode.RecreateTarget.Result)
				DiscardDeviceDependentResources();
			else
				hr.CheckError();
		}
		int FindNearestLineIndex(float y)
		{
			var distance = float.PositiveInfinity;
			var candidateKeyLineIndex = -1;
			for (var i = 0; i < m_KeyLines.Length; i++)
			{
				var bounds = m_Run.GetCharacterBounds(m_KeyLines[i][0][0]);
				var dist = Math.Abs(y + m_Run.LineSpacing.Height / 2 - bounds.Bottom);
				if (dist < distance)
				{
					distance = dist;
					candidateKeyLineIndex = i;
				}
			}
			return candidateKeyLineIndex;
		}
		int FindNearestSyllableIndex(int lineIndex, float x)
		{
			var distance = float.PositiveInfinity;
			var nearestIndex = 0;
			for (var i = 0; i < m_KeyLines[lineIndex].Length; i++)
			{
				foreach (var key in m_KeyLines[lineIndex][i])
				{
					var bounds = m_Run.GetCharacterBounds(key);
					var dist = Math.Abs(x - (bounds.Left + bounds.Right) / 2);
					if (dist < distance)
						(distance, nearestIndex) = (dist, i);
				}
			}
			return nearestIndex;
		}
		(int Line, int Column) HitTestPoint(Vector2 point)
		{
			var lineIndex = FindNearestLineIndex(point.Y);
			return lineIndex < 0 ? (0, 0) : (lineIndex, FindNearestSyllableIndex(lineIndex, point.X));
		}
		public void ScrollIntoPhysicalLineHead(int physicalLineIndex)
		{
			var index = new CharacterIndex(physicalLineIndex, -1, 0);
			ScrollInto(index, index);
		}
		void ScrollInto((int Line, int Column) index) => ScrollInto(m_KeyLines[index.Line][index.Column][0], m_KeyLines[index.Line][index.Column].Last());
		void ScrollInto(CharacterIndex leftIndex, CharacterIndex rightIndex)
		{
			var clientSize = ClientSize;
			var transform = ViewTransform;
			transform.Invert();
			var topLeft = Matrix3x2.TransformPoint(transform, new Vector2());
			var bottomRight = Matrix3x2.TransformPoint(transform, new Vector2(PixelToDip(clientSize.Width), PixelToDip(clientSize.Height) - NextLineViewerHeight));

			var mostLeftBounds = m_Run.GetCharacterBounds(leftIndex);
			var mostRightBounds = m_Run.GetCharacterBounds(rightIndex);
			var line = new RectangleF() { Left = mostLeftBounds.Left - s_LeftPadding, Top = mostLeftBounds.Bottom - m_Run.LineSpacing.Height - s_TopPadding, Right = mostRightBounds.Right + s_RightPadding, Bottom = mostLeftBounds.Bottom + s_BottomPadding };

			float offsetX = 0;
			if (line.Left < topLeft.X)
				offsetX += line.Left - topLeft.X;
			else if (line.Right > bottomRight.X)
				offsetX += line.Right - bottomRight.X;
			float offsetY = 0;
			if (line.Top < topLeft.Y)
				offsetY += line.Top - topLeft.Y;
			else if (line.Bottom > bottomRight.Y)
				offsetY += line.Bottom - bottomRight.Y;

			ScrollPositionX += DipToPixel(offsetX);
			ScrollPositionY += DipToPixel(offsetY);
		}

		public (int Line, int Column) Index
		{
			get => m_Index;
			set
			{
				if (m_Index != value)
				{
					m_Index = value;
					UpdateNextLineViewer();
				}
			}
		}
		protected override CreateParams CreateParams
		{
			get
			{
				const int WS_VSCROLL = 0x00200000;
				const int WS_HSCROLL = 0x00100000;
				var cp = base.CreateParams;
				cp.Style |= WS_VSCROLL | WS_HSCROLL;
				return cp;
			}
		}
		int ScrollPositionX
		{
			get => this.GetScrollInfo(ScrollBarKind.Horizontal, ScrollInfoMasks.Position).Position;
			set => this.SetScrollInfo(ScrollBarKind.Horizontal, position: value);
		}
		int ScrollPositionY
		{
			get => this.GetScrollInfo(ScrollBarKind.Vertical, ScrollInfoMasks.Position).Position;
			set => this.SetScrollInfo(ScrollBarKind.Vertical, position: value);
		}
		Matrix3x2 ViewTransform => Matrix3x2.Translation(-PixelToDip(ScrollPositionX) + s_LeftPadding, -PixelToDip(ScrollPositionY) + s_TopPadding);
		float NextLineViewerHeight => s_NextTopPadding + m_NextRun.Size.Height + s_NextBottomPadding;

		protected override void OnForeColorChanged(EventArgs e)
		{
			base.OnForeColorChanged(e);
			if (m_TextBrush != null)
				m_TextBrush.Color = ForeColor.ToColor4();
		}
		protected override void OnFontChanged(EventArgs e)
		{
			base.OnFontChanged(e);
			m_Run.ChangeFont(m_WriteFactory, Font);
			m_NextRun.ChangeFont(m_WriteFactory, Font);
		}
		protected override void OnDpiChangedAfterParent(EventArgs e)
		{
			base.OnDpiChangedAfterParent(e);
			if (m_RenderTarget != null) m_RenderTarget.DotsPerInch = new Size2F(DeviceDpi, DeviceDpi);
			UpdateScrollInfo();
			Invalidate();
		}
		protected sealed override void OnPaint(PaintEventArgs e)
		{
			if (DesignMode)
			{
				base.OnPaint(e);
				return;
			}
			BeginDraw();
			try
			{
				m_RenderTarget.Clear(BackColor.ToColor4());
				var transform = ViewTransform;
				m_RenderTarget.Transform = transform;
				if (m_KeyLines != null && m_KeyLines.Length > 0)
				{
					foreach (var key in m_KeyLines[Index.Line][Index.Column])
						m_RenderTarget.FillRectangle(m_Run.GetCharacterBounds(key), m_HighlightBrush);
				}
				m_Run.Draw(m_RenderTarget, m_TextBrush);
				m_RenderTarget.Transform = Matrix3x2.Identity;

				var size = m_RenderTarget.Size;
				m_RenderTarget.PushAxisAlignedClip(new RectangleF(0, size.Height - NextLineViewerHeight, size.Width, NextLineViewerHeight), AntialiasMode.Aliased);
				m_RenderTarget.Clear(Color.Gray);
				m_RenderTarget.Transform = Matrix3x2.Translation(transform.M31, size.Height - NextLineViewerHeight + s_NextTopPadding);
				m_NextRun.Draw(m_RenderTarget, m_NextTextBrush);
				m_RenderTarget.PopAxisAlignedClip();
			}
			finally { EndDraw(); }
		}
		protected sealed override void OnPaintBackground(PaintEventArgs e) { if (DesignMode) base.OnPaintBackground(e); }
		protected override void OnLayout(LayoutEventArgs levent)
		{
			base.OnLayout(levent);
			UpdateScrollInfo();
		}
		protected override void OnResize(EventArgs e)
		{
			base.OnResize(e);
			m_RenderTarget?.Resize(new Size2(ClientSize.Width, ClientSize.Height));
			Invalidate();
		}
		protected override void OnMouseClick(MouseEventArgs e)
		{
			base.OnMouseClick(e);
			if (e == null)
				throw new ArgumentNullException(nameof(e));
			Select();
			var transform = ViewTransform;
			transform.Invert();
			Index = HitTestPoint(Matrix3x2.TransformPoint(transform, new Vector2(PixelToDip(e.X), PixelToDip(e.Y))));
			Invalidate();
		}
		protected override void OnMouseWheel(MouseEventArgs e)
		{
			base.OnMouseWheel(e);
			var old = ScrollPositionY;
			var offset = -e.Delta / SystemInformation.MouseWheelScrollDelta * SystemInformation.MouseWheelScrollLines * DipToPixel(m_Run.LineSpacing.Height);
			if (this.SetScrollInfo(ScrollBarKind.Vertical, position: old + offset) != old)
				Invalidate();
		}
		protected override void OnKeyDown(KeyEventArgs e)
		{
			base.OnKeyDown(e);
			if (e == null)
				throw new ArgumentNullException(nameof(e));
			if (e.KeyCode == Keys.Left)
			{
				if (e.Control)
					HighlightFirst();
				else
					HighlightNext(false);
			}
			else if (e.KeyCode == Keys.Right)
			{
				if (e.Control)
					HighlightLast();
				else
					HighlightNext(true);
			}
			else if (e.KeyCode == Keys.Up || e.KeyCode == Keys.Down)
				HighlightNextLine(e.KeyCode == Keys.Down);
		}
		protected override bool IsInputKey(Keys keyData)
		{
			var keyCode = keyData & Keys.KeyCode;
			if (keyCode == Keys.Left || keyCode == Keys.Right || keyCode == Keys.Up || keyCode == Keys.Down)
				return true;
			return base.IsInputKey(keyData);
		}
		protected override void WndProc(ref Message m)
		{
			const int WM_HSCROLL = 0x0114;
			const int WM_VSCROLL = 0x0115;
			if ((m.Msg == WM_HSCROLL || m.Msg == WM_VSCROLL) && m.LParam == IntPtr.Zero)
			{
				OnScroll(m.Msg == WM_HSCROLL ? ScrollBarKind.Horizontal : ScrollBarKind.Vertical, (ScrollRequest)(m.WParam.ToInt64() & 0xffff));
				return;
			}
			base.WndProc(ref m);
		}

		void UpdateScrollInfo()
		{
			var size = m_Run.Size;
			// Add paddings
			size.Width += s_LeftPadding + s_RightPadding;
			size.Height += s_TopPadding + s_BottomPadding;
			// Add next line viewer height
			size.Height += NextLineViewerHeight;

			this.SetScrollInfo(ScrollBarKind.Horizontal, minimum: 0, maximum: DipToPixel(size.Width), pageSize: ClientSize.Width);
			this.SetScrollInfo(ScrollBarKind.Vertical, minimum: 0, maximum: DipToPixel(size.Height), pageSize: ClientSize.Height);
			// Because client size may be changed, redo
			this.SetScrollInfo(ScrollBarKind.Horizontal, pageSize: ClientSize.Width);
			this.SetScrollInfo(ScrollBarKind.Vertical, pageSize: ClientSize.Height);
		}
		void OnScroll(ScrollBarKind bar, ScrollRequest request)
		{
			var info = this.GetScrollInfo(bar);
			var newPos = info.Position;
			var lineHeight = DipToPixel(m_Run.LineSpacing.Height);
			switch (request)
			{
				case ScrollRequest.LineNear: newPos -= lineHeight; break;
				case ScrollRequest.LineFar: newPos += lineHeight; break;
				case ScrollRequest.PageNear: newPos -= info.PageSize; break;
				case ScrollRequest.PageFar: newPos += info.PageSize; break;
				case ScrollRequest.ThumbPosition:
				case ScrollRequest.ThumbTrack: newPos = info.TrackPosition; break;
				case ScrollRequest.Near: newPos = info.Minimum; break;
				case ScrollRequest.Far: newPos = info.Maximum; break;
			}
			if (this.SetScrollInfo(bar, position: newPos) != info.Position)
				Invalidate();
		}
	}

	abstract class Attached
	{
		public Attached(TextRange range) => Range = range;

		public TextRange Range { get; }

		public abstract void Recreate(DWriteFactory factory, TextFormat format);
		public abstract void Draw(RenderTarget renderTarget, Brush defaultFillBrush);
		public abstract float Measure(TextLayout baseTextLayout);
		public abstract void Arrange(TextLayout baseTextLayout);
		public abstract RectangleF GetCharacterBoundsIncludingBase(TextLayout baseTextLayout, int textPosition);
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
			m_TextLayout.TextAlignment = TextAlignment.Justified;
			if (m_TextLayout.GetClusterMetrics().Aggregate(0.0f, (x, y) => x + y.Width) < m_TextLayout.MaxWidth)
			{
				// text does not seem to be justified, so we use centering instead
				m_TextLayout.TextAlignment = TextAlignment.Center;
			}
			m_Origin = new Vector2(rangeMetrics.Left + spacing / 2 / nonWhitespaceClusterCount, rangeMetrics.Top);
		}
		public override RectangleF GetCharacterBoundsIncludingBase(TextLayout baseTextLayout, int textPosition)
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
		public override AttachedSpecifier CreateSpecifier() => new RubySpecifier(new TextRange(Range.StartPosition, Range.Length), Text);

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
		public override RectangleF GetCharacterBoundsIncludingBase(TextLayout baseTextLayout, int textPosition)
		{
			var rangeMetrics = GetMetricsForRange(baseTextLayout);
			var metrics = baseTextLayout.HitTestTextPosition(Range.StartPosition, false, out var _, out var _);
			return new RectangleF(rangeMetrics.Left + rangeMetrics.Width * textPosition / DivisionCount, metrics.Top, rangeMetrics.Width / DivisionCount, rangeMetrics.Top + rangeMetrics.Height - metrics.Top);
		}
		public override AttachedSpecifier CreateSpecifier() => new SyllableDivisionSpecifier(Range, DivisionCount);
	}

	class TextRun : IDisposable
	{
		public TextRun(DWriteFactory writeFacotry, System.Drawing.Font font) => Setup(writeFacotry, Enumerable.Empty<AttachedLine>(), font);
		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}
		protected virtual void Dispose(bool disposing)
		{
			if (!disposing)
				return;
			m_Text = null;
			Utils.SafeDispose(ref m_TextLayout);
			m_LineIndexes = null;
			CleanupAttacheds();
		}

		string m_Text;
		TextLayout m_TextLayout;
		(int Text, int Attached)[] m_LineIndexes;
		Attached[] m_Attacheds;

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
		public LineSpacing LineSpacing
		{
			get
			{
				m_TextLayout.GetLineSpacing(out var method, out var lineSpacing, out var baseline);
				return new LineSpacing() { Method = method, Height = lineSpacing, Baseline = baseline };
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
		public void Setup(DWriteFactory writeFactory, IEnumerable<AttachedLine> attachedLines, System.Drawing.Font font)
		{
			if (attachedLines == null)
				throw new ArgumentNullException(nameof(attachedLines));
			var lineIndexes = new List<(int Text, int Attached)>();
			var textBuilder = new StringBuilder();
			var attachedList = new List<AttachedSpecifier>();
			var first = true;
			foreach (var attachedLine in attachedLines)
			{
				if (!first)
					textBuilder.Append('\n');
				lineIndexes.Add((textBuilder.Length, attachedList.Count));
				attachedList.AddRange(attachedLine.AttachedSpecifiers.Select(x => x.Move(textBuilder.Length)));
				textBuilder.Append(attachedLine.Text);
				first = false;
			}
			var (fontFamilyName, fontWeight, fontStyle, fontStretch, fontSize) = Utils.GetFontFromDrawingFont(writeFactory, font);
			m_Text = textBuilder.ToString();
			m_LineIndexes = lineIndexes.ToArray();
			using (var format = new TextFormat(writeFactory, fontFamilyName, fontWeight, fontStyle, fontStretch, fontSize))
			{
				format.WordWrapping = WordWrapping.NoWrap;
				format.SetLineSpacing(LineSpacingMethod.Uniform, fontSize * 1.5f * (1.0f / 0.8f), fontSize * 1.5f);
				Utils.AssignWithDispose(ref m_TextLayout, new TextLayout(writeFactory, m_Text, format, 0f, 0f));
			}
			CleanupAttacheds();
			m_Attacheds = new Attached[attachedList.Count];
			using (var format = new TextFormat(writeFactory, fontFamilyName, fontWeight, fontStyle, fontStretch, fontSize / 2))
			{
				format.WordWrapping = WordWrapping.NoWrap;
				for (var i = 0; i < attachedList.Count; i++)
				{
					m_Attacheds[i] = attachedList[i].Create(writeFactory, format);
					var spacing = m_Attacheds[i].Measure(m_TextLayout);
					SetRangeSpacing(spacing, spacing, 0, m_Attacheds[i].Range);
					m_Attacheds[i].Arrange(m_TextLayout);
				}
			}
		}
		public void ChangeFont(DWriteFactory writeFactory, System.Drawing.Font font)
		{
			var (fontFamilyName, fontWeight, fontStyle, fontStretch, fontSize) = Utils.GetFontFromDrawingFont(writeFactory, font);
			using (var format = new TextFormat(writeFactory, fontFamilyName, fontWeight, fontStyle, fontStretch, fontSize))
			{
				format.WordWrapping = WordWrapping.NoWrap;
				format.SetLineSpacing(LineSpacingMethod.Uniform, fontSize * 1.5f * (1.0f / 0.8f), fontSize * 1.5f);
				Utils.AssignWithDispose(ref m_TextLayout, new TextLayout(writeFactory, m_Text, format, 0f, 0f));
			}
			using (var format = new TextFormat(writeFactory, fontFamilyName, fontWeight, fontStyle, fontStretch, fontSize / 2))
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
		public RectangleF GetCharacterBounds(CharacterIndex index)
		{
			if (m_TextLayout == null)
				throw new InvalidOperationException("Setup must be called before calling GetCharacterBounds.");
			if (index.IsSimple)
			{
				// HitTestTextPoint retrieves actual character bounds, while HitTestTextRange retrieves line-based bounds.
				var metrics = m_TextLayout.HitTestTextPosition(m_LineIndexes[index.Line].Text + index.Character, false, out var _, out var _);
				var rangeMetrics = m_TextLayout.HitTestTextRange(metrics.TextPosition, metrics.Length, 0f, 0f);
				if (rangeMetrics.Length > 1)
					throw new InvalidOperationException("One index must references one script group.");
				return new RectangleF(rangeMetrics[0].Left, metrics.Top, rangeMetrics[0].Width, rangeMetrics[0].Top + rangeMetrics[0].Height - metrics.Top);
			}
			else
				return m_Attacheds[m_LineIndexes[index.Line].Attached + index.Attached].GetCharacterBoundsIncludingBase(m_TextLayout, index.Character);
		}
		public AttachedLine GetLine(int line)
		{
			var (textEndIndex, attachedEndIndex) = line + 1 >= m_LineIndexes.Length ? (m_Text.Length, m_Attacheds.Length) : m_LineIndexes[line + 1];
			return new AttachedLine(
				m_Text.Substring(m_LineIndexes[line].Text, textEndIndex - m_LineIndexes[line].Text).TrimEnd('\n'),
				m_Attacheds.Skip(m_LineIndexes[line].Attached).Take(attachedEndIndex - m_LineIndexes[line].Attached).Select(x => x.CreateSpecifier().Move(-m_LineIndexes[line].Text)).ToArray()
			);
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

	public readonly struct AttachedLine
	{
		[CLSCompliant(false)]
		public AttachedLine(string text, IEnumerable<AttachedSpecifier> attachedSpecifiers)
		{
			Text = text;
			AttachedSpecifiers = attachedSpecifiers.ToArray();
		}

		public string Text { get; }

		[CLSCompliant(false)]
		public IReadOnlyList<AttachedSpecifier> AttachedSpecifiers { get; }
	}

	[CLSCompliant(false)]
	public abstract class AttachedSpecifier
	{
		protected AttachedSpecifier(TextRange range) => Range = range;

		public TextRange Range { get; }

		public abstract AttachedSpecifier Move(int distance);

		internal abstract Attached Create(DWriteFactory writeFactory, TextFormat format);
	}

	[CLSCompliant(false)]
	public class RubySpecifier : AttachedSpecifier
	{
		public RubySpecifier(TextRange range, string text) : base(range) => Text = text;

		public string Text { get; }

		public override AttachedSpecifier Move(int distance) => new RubySpecifier(new TextRange(Range.StartPosition + distance, Range.Length), Text);

		internal override Attached Create(DWriteFactory writeFactory, TextFormat format) => new Ruby(writeFactory, Range, Text, format);
	}

	[CLSCompliant(false)]
	public class SyllableDivisionSpecifier : AttachedSpecifier
	{
		public SyllableDivisionSpecifier(TextRange range, int divisionCount) : base(range) => DivisionCount = divisionCount;

		public int DivisionCount { get; }

		public override AttachedSpecifier Move(int distance) => new SyllableDivisionSpecifier(new TextRange(Range.StartPosition + distance, Range.Length), DivisionCount);

		internal override Attached Create(DWriteFactory writeFactory, TextFormat format) => new SyllableDivision(Range, DivisionCount);
	}

	public readonly struct CharacterIndex
	{
		public CharacterIndex(int line, int attached, int character)
		{
			Line = line;
			Attached = attached;
			Character = character;
		}

		public int Line { get; }
		public int Attached { get; }
		public int Character { get; }
		public bool IsSimple => Attached < 0;
	}

	static class Utils
	{
		public static void AssignWithDispose<T>(ref T storage, T value) where T : IDisposable
		{
			storage?.Dispose();
			storage = value;
		}
		public static void SafeDispose<T>(ref T storage) where T : IDisposable => AssignWithDispose(ref storage, default);
		public static Color4 ToColor4(this System.Drawing.Color color) => new Color4(color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f);
		public static (string FamilyName, FontWeight Weight, FontStyle Style, FontStretch Stretch, float Size) GetFontFromDrawingFont(DWriteFactory factory, System.Drawing.Font drawingFont)
		{
			var logfont = new GdiInterop.LogFont();
			drawingFont.ToLogFont(logfont);
			using (var gdiInterop = factory.GdiInterop)
			using (var font = gdiInterop.FromLogFont(logfont))
			{
				string fontFamilyName;
				using (var fontFamily = font.FontFamily)
				using (var familyNames = fontFamily.FamilyNames)
					fontFamilyName = familyNames.GetString(0);
				var fontWeight = font.Weight;
				var fontStyle = font.Style;
				var fontStretch = font.Stretch;
				var fontSize = drawingFont.SizeInPoints * 96f / 72f;
				if ((font.Simulations & FontSimulations.Bold) != FontSimulations.None)
					fontWeight = FontWeight.Bold;
				if ((font.Simulations & FontSimulations.Oblique) != FontSimulations.None)
					fontStyle = FontStyle.Oblique;
				return (fontFamilyName, fontWeight, fontStyle, fontStretch, fontSize);
			}
		}
	}

	[StructLayout(LayoutKind.Sequential)]
	struct ScrollInfo
	{
		public int Size;
		public ScrollInfoMasks Mask;
		public int Minimum;
		public int Maximum;
		public int PageSize;
		public int Position;
		public int TrackPosition;
	}

	static class NativeMethods
	{
		[DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
		static extern bool GetScrollInfo(HandleRef hwnd, ScrollBarKind bar, [In, Out] ref ScrollInfo si);
		[DllImport("user32.dll", ExactSpelling = true)]
		static extern int SetScrollInfo(HandleRef hwnd, ScrollBarKind bar, [In] in ScrollInfo si, [MarshalAs(UnmanagedType.Bool)] bool redraw);

		public static ScrollInfo GetScrollInfo(this Control control, ScrollBarKind bar, ScrollInfoMasks mask = ScrollInfoMasks.All)
		{
			ScrollInfo si = default;
			si.Size = Marshal.SizeOf<ScrollInfo>();
			si.Mask = mask;
			if (!GetScrollInfo(new HandleRef(control, control.Handle), bar, ref si))
				Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
			return si;
		}
		public static int SetScrollInfo(this Control control, ScrollBarKind bar, bool redraw = true, bool disableNoScroll = false, int? minimum = default, int? maximum = default, int? pageSize = default, int? position = default)
		{
			ScrollInfo si = default;
			si.Size = Marshal.SizeOf<ScrollInfo>();
			si.Mask = 0;
			if (minimum.HasValue || maximum.HasValue)
			{
				si.Mask |= ScrollInfoMasks.Range;
				si.Minimum = minimum.GetValueOrDefault();
				si.Maximum = maximum.GetValueOrDefault();
			}
			if (pageSize.HasValue)
			{
				si.Mask |= ScrollInfoMasks.PageSize;
				si.PageSize = pageSize.Value;
			}
			if (position.HasValue)
			{
				si.Mask |= ScrollInfoMasks.Position;
				si.Position = position.Value;
			}
			if (disableNoScroll)
				si.Mask |= ScrollInfoMasks.DisableNoScroll;
			return SetScrollInfo(new HandleRef(control, control.Handle), bar, si, redraw);
		}
	}

	[Flags]
	enum ScrollInfoMasks : int
	{
		Range = 0x0001,
		PageSize = 0x0002,
		Position = 0x0004,
		DisableNoScroll = 0x0008,
		TrackPosition = 0x0010,
		All = Range | PageSize | Position | TrackPosition,
	}

	enum ScrollBarKind : int
	{
		Horizontal = 0,
		Vertical = 1,
		Control = 2,
		Both = 3,
	}

	enum ScrollRequest : int
	{
		LineNear = 0,
		LineFar = 1,
		PageNear = 2,
		PageFar = 3,
		ThumbPosition = 4,
		ThumbTrack = 5,
		Near = 6,
		Far = 7,
		EndScroll = 8,
	}
}
