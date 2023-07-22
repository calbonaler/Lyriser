using System;
using System.Runtime.Versioning;
using Windows.Win32.Foundation;
using PInvoke = Windows.Win32.PInvoke;
using IUnknown = Windows.Win32.System.Com.IUnknown;
using DWrite = Windows.Win32.Graphics.DirectWrite;

namespace Lyriser.Core.DirectWrite;

public enum FontStyle
{
	Normal = DWrite.DWRITE_FONT_STYLE.DWRITE_FONT_STYLE_NORMAL,
	Oblique = DWrite.DWRITE_FONT_STYLE.DWRITE_FONT_STYLE_OBLIQUE,
	Italic = DWrite.DWRITE_FONT_STYLE.DWRITE_FONT_STYLE_ITALIC,
}

public enum WordWrapping
{
	/// <summary>Words are broken across lines to avoid text overflowing the layout box.</summary>
	Wrap = DWrite.DWRITE_WORD_WRAPPING.DWRITE_WORD_WRAPPING_WRAP,
	/// <summary>
	/// Words are kept within the same line even when it overflows the layout box.
	/// This option is often used with scrolling to reveal overflow text. 
	/// </summary>
	NoWrap = DWrite.DWRITE_WORD_WRAPPING.DWRITE_WORD_WRAPPING_NO_WRAP,
	/// <summary>
	/// Words are broken across lines to avoid text overflowing the layout box.
	/// Emergency wrapping occurs if the word is larger than the maximum width.
	/// </summary>
	EmergencyBreak = DWrite.DWRITE_WORD_WRAPPING.DWRITE_WORD_WRAPPING_EMERGENCY_BREAK,
	/// <summary>Only wrap whole words, never breaking words (emergency wrapping) when the layout width is too small for even a single word.</summary>
	WholeWord = DWrite.DWRITE_WORD_WRAPPING.DWRITE_WORD_WRAPPING_WHOLE_WORD,
	/// <summary>Wrap between any valid characters clusters.</summary>
	Character = DWrite.DWRITE_WORD_WRAPPING.DWRITE_WORD_WRAPPING_CHARACTER,
};

public enum LineSpacingMethod
{
	/// <summary>Line spacing depends solely on the content, growing to accommodate the size of fonts and inline objects.</summary>
	Default = DWrite.DWRITE_LINE_SPACING_METHOD.DWRITE_LINE_SPACING_METHOD_DEFAULT,
	/// <summary>
	/// Lines are explicitly set to uniform spacing, regardless of contained font sizes.
	/// This can be useful to avoid the uneven appearance that can occur from font fallback.
	/// </summary>
	Uniform = DWrite.DWRITE_LINE_SPACING_METHOD.DWRITE_LINE_SPACING_METHOD_UNIFORM,
	/// <summary>Line spacing and baseline distances are proportional to the computed values based on the content, the size of the fonts and inline objects.</summary>
	Proportional = DWrite.DWRITE_LINE_SPACING_METHOD.DWRITE_LINE_SPACING_METHOD_PROPORTIONAL
};

/// <summary>Alignment of paragraph text along the reading direction axis relative to the leading and trailing edge of the layout box.</summary>
public enum TextAlignment
{
	/// <summary>The leading edge of the paragraph text is aligned to the layout box's leading edge.</summary>
	Leading = DWrite.DWRITE_TEXT_ALIGNMENT.DWRITE_TEXT_ALIGNMENT_LEADING,
	/// <summary>The trailing edge of the paragraph text is aligned to the layout box's trailing edge.</summary>
	Trailing = DWrite.DWRITE_TEXT_ALIGNMENT.DWRITE_TEXT_ALIGNMENT_TRAILING,
	/// <summary>The center of the paragraph text is aligned to the center of the layout box.</summary>
	Center = DWrite.DWRITE_TEXT_ALIGNMENT.DWRITE_TEXT_ALIGNMENT_CENTER,
	/// <summary>Align text to the leading side, and also justify text to fill the lines.</summary>
	Justified = DWrite.DWRITE_TEXT_ALIGNMENT.DWRITE_TEXT_ALIGNMENT_JUSTIFIED
};

/// <summary>The DWRITE_TEXT_RANGE structure specifies a range of text positions where format is applied.</summary>
public record struct TextRange
{
	public static TextRange FromStartLength(int startPosition, int length) => new() { StartPosition = startPosition, Length = length };
	public static TextRange FromStartEnd(int startPosition, int endPosition) => FromStartLength(startPosition, endPosition - startPosition);

	/// <summary>The end text position of the range.</summary>
	public readonly int EndPosition => StartPosition + Length;

	/// <summary>The start text position of the range.</summary>
	public int StartPosition;
	/// <summary>The number of text positions in the range.</summary>
	public int Length;

	internal DWrite.DWRITE_TEXT_RANGE ToNative() => new() { startPosition = (uint)StartPosition, length = (uint)Length };
};

/// <summary>The DWRITE_CLUSTER_METRICS structure contains information about a glyph cluster.</summary>
public record struct ClusterMetrics
{
	/// <summary>The total advance width of all glyphs in the cluster.</summary>
	public float Width;
	/// <summary>The number of text positions in the cluster.</summary>
	public short Length;
	short bitValues;

	/// <summary>Indicate whether line can be broken right after the cluster.</summary>
	public readonly bool CanWrapLineAfter => (bitValues & 1) != 0;
	/// <summary>Indicate whether the cluster corresponds to whitespace character.</summary>
	public readonly bool IsWhitespace => (bitValues & 2) != 0;
	/// <summary>Indicate whether the cluster corresponds to a newline character.</summary>
	public readonly bool IsNewline => (bitValues & 4) != 0;
	/// <summary>Indicate whether the cluster corresponds to soft hyphen character.</summary>
	public readonly bool IsSoftHyphen => (bitValues & 8) != 0;
	/// <summary>Indicate whether the cluster is read from right to left.</summary>
	public readonly bool IsRightToLeft => (bitValues & 16) != 0;
};

/// <summary>
/// Overall metrics associated with text after layout.
/// All coordinates are in device independent pixels (DIPs).
/// </summary>
public record struct TextMetrics
{
	/// <summary>Top-left point of formatted text relative to layout box (excluding any glyph overhang).</summary>
	public System.Numerics.Vector2 TopLeft;
	/// <summary>The width of the formatted text ignoring trailing whitespace at the end of each line.</summary>
	public float Width;
	/// <summary>The width of the formatted text taking into account the trailing whitespace at the end of each line.</summary>
	public float WidthIncludingTrailingWhitespace;
	/// <summary>The height of the formatted text. The height of an empty string is determined by the size of the default font's line height.</summary>
	public float Height;
	/// <summary>Initial size given to the layout. Depending on whether the text was wrapped or not and the length of the text, it may be larger or smaller than the text content size.</summary>
	public System.Numerics.Vector2 LayoutSize;
	/// <summary>
	/// The maximum reordering count of any line of text, used to calculate the most number of hit-testing boxes needed.
	/// If the layout has no bidirectional text or no text at all, the minimum level is 1.
	/// </summary>
	public int MaxBidiReorderingDepth;
	/// <summary>Total number of lines.</summary>
	public int LineCount;
}

/// <summary>Geometry enclosing of text positions.</summary>
public record struct HitTestMetrics
{
	/// <summary>Text range within the geometry.</summary>
	public TextRange TextRange;
	/// <summary>Position of the top-left coordinate of the geometry.</summary>
	public System.Numerics.Vector2 TopLeft;
	/// <summary>Geometry's size.</summary>
	public System.Numerics.Vector2 Size;
	/// <summary>Bidi level of text positions enclosed within the geometry.</summary>
	public int BidiLevel;
	int isText;
	int isTrimmed;

	/// <summary>Geometry encloses text?</summary>
	public readonly bool IsText => isText != 0;
	/// <summary>Range is trimmed.</summary>
	public readonly bool IsTrimmed => isTrimmed != 0;
	public readonly System.Numerics.Vector2 BottomRight => TopLeft + Size;
};

public unsafe class TextFormat : Interop.DisposableComPtrBase
{
	internal TextFormat() { }

	public (LineSpacingMethod LineSpacingMethod, float LineSpacing, float Baseline) LineSpacing
	{
		get
		{
			Pointer->GetLineSpacing(out var lineSpacingMethod, out var lineSpacing, out var baseline);
			return ((LineSpacingMethod)lineSpacingMethod, lineSpacing, baseline);
		}
		set => Pointer->SetLineSpacing((DWrite.DWRITE_LINE_SPACING_METHOD)value.LineSpacingMethod, value.LineSpacing, value.Baseline);
	}
	public WordWrapping WordWrapping
	{
		get => (WordWrapping)Pointer->GetWordWrapping();
		set => Pointer->SetWordWrapping((DWrite.DWRITE_WORD_WRAPPING)value);
	}

	internal new DWrite.IDWriteTextFormat* Pointer => (DWrite.IDWriteTextFormat*)base.Pointer;
	internal new ref DWrite.IDWriteTextFormat* Put() => ref Interop.ComUtils.As<IUnknown, DWrite.IDWriteTextFormat>(ref base.Put());
}

public unsafe class TextLayout : TextFormat
{
	internal TextLayout() { }

	public ClusterMetrics[] GetClusterMetrics()
	{
		var hr = Pointer->GetClusterMetrics(default, out var actualClusterCount);
		if (hr != PInvoke.HRESULT_FROM_WIN32(WIN32_ERROR.ERROR_INSUFFICIENT_BUFFER))
			hr.ThrowOnFailure();
		if (actualClusterCount == 0) return Array.Empty<ClusterMetrics>();
		var clusterMetrics = new ClusterMetrics[actualClusterCount];
		fixed (ClusterMetrics* ptr = &clusterMetrics[0])
			Pointer->GetClusterMetrics((DWrite.DWRITE_CLUSTER_METRICS*)ptr, actualClusterCount, &actualClusterCount).ThrowOnFailure();
		return clusterMetrics;
	}
	public (System.Numerics.Vector2 Point, HitTestMetrics HitTestMetrics) HitTestTextPosition(int textPosition, bool isTrailingHit)
	{
		var hitTestMetrics = new HitTestMetrics();
		var pointX = 0f;
		var pointY = 0f;
		Pointer->HitTestTextPosition((uint)textPosition, isTrailingHit, &pointX, &pointY, (DWrite.DWRITE_HIT_TEST_METRICS*)&hitTestMetrics);
		return (new System.Numerics.Vector2(pointX, pointY), hitTestMetrics);
	}
	public bool HitTestTextRange(TextRange textRange, System.Numerics.Vector2 origin, out HitTestMetrics hitTestMetrics)
	{
		HRESULT hr;
		var actualHitTestMetricsCount = 0u;
		fixed (HitTestMetrics* pHitTestMetrics = &hitTestMetrics)
			hr = Pointer->HitTestTextRange((uint)textRange.StartPosition, (uint)textRange.Length, origin.X, origin.Y, (DWrite.DWRITE_HIT_TEST_METRICS*)pHitTestMetrics, 1, &actualHitTestMetricsCount);
		if (hr == PInvoke.HRESULT_FROM_WIN32(WIN32_ERROR.ERROR_INSUFFICIENT_BUFFER))
			return false;
		hr.ThrowOnFailure();
		return true;
	}
	[SupportedOSPlatform("windows8.0")]
	public void SetCharacterSpacing(float leadingSpacing, float trailingSpacing, float minimumAdvanceWidth, TextRange textRange)
	{
		using var textLayout1 = Interop.ComUtils.Cast<DWrite.IDWriteTextLayout1>(Pointer);
		textLayout1.Pointer->SetCharacterSpacing(leadingSpacing, trailingSpacing, minimumAdvanceWidth, textRange.ToNative());
	}

	public float MaxWidth
	{
		get => Pointer->GetMaxWidth();
		set => Pointer->SetMaxWidth(value);
	}
	public TextMetrics Metrics
	{
		get
		{
			var textMetrics = new TextMetrics();
			Pointer->GetMetrics((DWrite.DWRITE_TEXT_METRICS*)&textMetrics);
			return textMetrics;
		}
	}
	public TextAlignment TextAlignment
	{
		get => (TextAlignment)Pointer->GetTextAlignment();
		set => Pointer->SetTextAlignment((DWrite.DWRITE_TEXT_ALIGNMENT)value);
	}

	internal new DWrite.IDWriteTextLayout* Pointer => (DWrite.IDWriteTextLayout*)base.Pointer;
	internal new ref DWrite.IDWriteTextLayout* Put() => ref Interop.ComUtils.As<DWrite.IDWriteTextFormat, DWrite.IDWriteTextLayout>(ref base.Put());
}

public unsafe class Factory : Interop.DisposableComPtrBase
{
	public Factory() =>
		PInvoke.DWriteCreateFactory(DWrite.DWRITE_FACTORY_TYPE.DWRITE_FACTORY_TYPE_SHARED, typeof(DWrite.IDWriteFactory).GUID, out PutVoid()).ThrowOnFailure();

	public unsafe TextFormat CreateTextFormat(string fontFamilyName, int fontWeight, FontStyle fontStyle, int fontStretch, float fontSize)
	{
		var emptyString = '\0';
		var result = new TextFormat();
		fixed (DWrite.IDWriteTextFormat** ppResult = &result.Put())
		fixed (char* pFontFamilyName = fontFamilyName)
		{
			Pointer->CreateTextFormat(pFontFamilyName, null,
				(DWrite.DWRITE_FONT_WEIGHT)fontWeight, (DWrite.DWRITE_FONT_STYLE)fontStyle, (DWrite.DWRITE_FONT_STRETCH)fontStretch, fontSize,
				&emptyString, ppResult);
		}
		return result;
	}
	public unsafe TextLayout CreateTextLayout(string text, TextFormat textFormat, System.Numerics.Vector2 maxSize)
	{
		var result = new TextLayout();
		fixed (DWrite.IDWriteTextLayout ** ppResult = &result.Put())
		fixed (char* pText = text)
			Pointer->CreateTextLayout(pText, (uint)text.Length, textFormat.Pointer, maxSize.X, maxSize.Y, ppResult);
		return result;
	}

	internal new DWrite.IDWriteFactory* Pointer => (DWrite.IDWriteFactory*)base.Pointer;
};
