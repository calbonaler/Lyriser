#pragma once

#include "Common.h"
#include "Interop.h"

namespace Lyriser::Core::DirectWrite
{
	public enum class FontStyle
	{
		Normal = DWRITE_FONT_STYLE_NORMAL,
		Oblique = DWRITE_FONT_STYLE_OBLIQUE,
		Italic = DWRITE_FONT_STYLE_ITALIC,
	};

	public enum class WordWrapping
	{
		/// <summary>Words are broken across lines to avoid text overflowing the layout box.</summary>
		Wrap = DWRITE_WORD_WRAPPING_WRAP,
		/// <summary>
		/// Words are kept within the same line even when it overflows the layout box.
		/// This option is often used with scrolling to reveal overflow text. 
		/// </summary>
		NoWrap = DWRITE_WORD_WRAPPING_NO_WRAP,
		/// <summary>
		/// Words are broken across lines to avoid text overflowing the layout box.
		/// Emergency wrapping occurs if the word is larger than the maximum width.
		/// </summary>
		EmergencyBreak = DWRITE_WORD_WRAPPING_EMERGENCY_BREAK,
		/// <summary>Only wrap whole words, never breaking words (emergency wrapping) when the layout width is too small for even a single word.</summary>
		WholeWord = DWRITE_WORD_WRAPPING_WHOLE_WORD,
		/// <summary>Wrap between any valid characters clusters.</summary>
		Character = DWRITE_WORD_WRAPPING_CHARACTER,
	};

	public enum class LineSpacingMethod
	{
		/// <summary>Line spacing depends solely on the content, growing to accommodate the size of fonts and inline objects.</summary>
		Default = DWRITE_LINE_SPACING_METHOD_DEFAULT,
		/// <summary>
		/// Lines are explicitly set to uniform spacing, regardless of contained font sizes.
		/// This can be useful to avoid the uneven appearance that can occur from font fallback.
		/// </summary>
		Uniform = DWRITE_LINE_SPACING_METHOD_UNIFORM,
		/// <summary>Line spacing and baseline distances are proportional to the computed values based on the content, the size of the fonts and inline objects.</summary>
		Proportional = DWRITE_LINE_SPACING_METHOD_PROPORTIONAL
	};

	/// <summary>Alignment of paragraph text along the reading direction axis relative to the leading and trailing edge of the layout box.</summary>
	public enum class TextAlignment
	{
		/// <summary>The leading edge of the paragraph text is aligned to the layout box's leading edge.</summary>
		Leading = DWRITE_TEXT_ALIGNMENT_LEADING,
		/// <summary>The trailing edge of the paragraph text is aligned to the layout box's trailing edge.</summary>
		Trailing = DWRITE_TEXT_ALIGNMENT_TRAILING,
		/// <summary>The center of the paragraph text is aligned to the center of the layout box.</summary>
		Center = DWRITE_TEXT_ALIGNMENT_CENTER,
		/// <summary>Align text to the leading side, and also justify text to fill the lines.</summary>
		Justified = DWRITE_TEXT_ALIGNMENT_JUSTIFIED
	};

	/// <summary>The DWRITE_TEXT_RANGE structure specifies a range of text positions where format is applied.</summary>
	public value struct TextRange
	{
	public:
		static TextRange FromStartLength(int startPosition, int length) { return { startPosition, length }; }
		static TextRange FromStartEnd(int startPosition, int endPosition) { return { startPosition, endPosition - startPosition }; }

		/// <summary>The end text position of the range.</summary>
		property int EndPosition { int get() { return StartPosition + Length; } }

		/// <summary>The start text position of the range.</summary>
		int StartPosition;
		/// <summary>The number of text positions in the range.</summary>
		int Length;

	internal:
		DWRITE_TEXT_RANGE ToNative() { return { static_cast<UINT32>(StartPosition), static_cast<UINT32>(Length) }; }
	};

	/// <summary>The DWRITE_CLUSTER_METRICS structure contains information about a glyph cluster.</summary>
	public value struct ClusterMetrics
	{
	public:
		/// <summary>The total advance width of all glyphs in the cluster.</summary>
		float Width;
		/// <summary>The number of text positions in the cluster.</summary>
		short Length;
	private:
		short bitValues;
	public:
		/// <summary>Indicate whether line can be broken right after the cluster.</summary>
		property bool CanWrapLineAfter { bool get() { return bitValues & 1; } }
		/// <summary>Indicate whether the cluster corresponds to whitespace character.</summary>
		property bool IsWhitespace { bool get() { return bitValues & 2; } }
		/// <summary>Indicate whether the cluster corresponds to a newline character.</summary>
		property bool IsNewline { bool get() { return bitValues & 4; } }
		/// <summary>Indicate whether the cluster corresponds to soft hyphen character.</summary>
		property bool IsSoftHyphen { bool get() { return bitValues & 8; } }
		/// <summary>Indicate whether the cluster is read from right to left.</summary>
		property bool IsRightToLeft { bool get() { return bitValues & 16; } }
	};

	/// <summary>
	/// Overall metrics associated with text after layout.
	/// All coordinates are in device independent pixels (DIPs).
	/// </summary>
	public value struct TextMetrics
	{
	public:
		/// <summary>Top-left point of formatted text relative to layout box (excluding any glyph overhang).</summary>
		Direct2D1::Point2F Position;
		/// <summary>The width of the formatted text ignoring trailing whitespace at the end of each line.</summary>
		float Width;
		/// <summary>The width of the formatted text taking into account the trailing whitespace at the end of each line.</summary>
		float WidthIncludingTrailingWhitespace;
		/// <summary>The height of the formatted text. The height of an empty string is determined by the size of the default font's line height.</summary>
		float Height;
		/// <summary>Initial size given to the layout. Depending on whether the text was wrapped or not and the length of the text, it may be larger or smaller than the text content size.</summary>
		Direct2D1::SizeF LayoutSize;
		/// <summary>
		/// The maximum reordering count of any line of text, used to calculate the most number of hit-testing boxes needed.
		/// If the layout has no bidirectional text or no text at all, the minimum level is 1.
		/// </summary>
		int MaxBidiReorderingDepth;
		/// <summary>Total number of lines.</summary>
		int LineCount;
	};

	/// <summary>Geometry enclosing of text positions.</summary>
	public value struct HitTestMetrics
	{
	public:
		/// <summary>Text range within the geometry.</summary>
		TextRange TextRange;
		/// <summary>Position of the top-left coordinate of the geometry.</summary>
		Direct2D1::Point2F Position;
		/// <summary>Geometry's size.</summary>
		Direct2D1::SizeF Size;
		/// <summary>Bidi level of text positions enclosed within the geometry.</summary>
		int BidiLevel;
	private:
		int isText;
		int isTrimmed;
	public:
		/// <summary>Geometry encloses text?</summary>
		property bool IsText { bool get() { return isText; } }
		/// <summary>Range is trimmed.</summary>
		property bool IsTrimmed { bool get() { return isTrimmed; } }
	};

	public ref class TextFormat : public Interop::DisposableComPtrBase<IDWriteTextFormat>
	{
	public:
		[System::Runtime::CompilerServices::TupleElementNamesAttribute(gcnew cli::array<System::String^>() { "LineSpacingMethod", "LineSpacing", "Baseline" })]
		property System::ValueTuple<LineSpacingMethod, float, float> LineSpacing
		{
			System::ValueTuple<LineSpacingMethod, float, float> get()
			{
				LineSpacingMethod lineSpacingMethod = LineSpacingMethod::Default;
				float lineSpacing;
				float baseline;
				ThrowIfFailed(p->GetLineSpacing(reinterpret_cast<DWRITE_LINE_SPACING_METHOD*>(&lineSpacingMethod), &lineSpacing, &baseline));
				return System::ValueTuple<LineSpacingMethod, float, float>(lineSpacingMethod, lineSpacing, baseline);
			}
			void set(System::ValueTuple<LineSpacingMethod, float, float> value) { ThrowIfFailed(p->SetLineSpacing(safe_cast<DWRITE_LINE_SPACING_METHOD>(value.Item1), value.Item2, value.Item3)); }
		}
		property WordWrapping WordWrapping
		{
			DirectWrite::WordWrapping get() { return safe_cast<DirectWrite::WordWrapping>(p->GetWordWrapping()); }
			void set(DirectWrite::WordWrapping value) { ThrowIfFailed(p->SetWordWrapping(safe_cast<DWRITE_WORD_WRAPPING>(value))); }
		}

	internal:
		TextFormat() {}
	};

	public ref class TextLayout : public TextFormat
	{
	public:
		cli::array<ClusterMetrics>^ GetClusterMetrics()
		{
			UINT32 actualClusterCount;
			auto hr = GetPointer()->GetClusterMetrics(nullptr, 0, &actualClusterCount);
			if (hr != E_NOT_SUFFICIENT_BUFFER) ThrowIfFailed(hr);
			if (actualClusterCount == 0) return System::Array::Empty<ClusterMetrics>();
			cli::array<ClusterMetrics>^ clusterMetrics = gcnew cli::array<ClusterMetrics>(actualClusterCount);
			pin_ptr<ClusterMetrics> pClusterMetrics = &clusterMetrics[0];
			ThrowIfFailed(GetPointer()->GetClusterMetrics(reinterpret_cast<DWRITE_CLUSTER_METRICS*>(pClusterMetrics), actualClusterCount, &actualClusterCount));
			return clusterMetrics;
		}
		[returnvalue: System::Runtime::CompilerServices::TupleElementNamesAttribute(gcnew cli::array<System::String^>() { "Point", "HitTestMetrics" })]
		System::ValueTuple<Direct2D1::Point2F, HitTestMetrics> HitTestTextPosition(int textPosition, bool isTrailingHit)
		{
			float pointX;
			float pointY;
			HitTestMetrics hitTestMetrics{};
			ThrowIfFailed(GetPointer()->HitTestTextPosition(textPosition, isTrailingHit, &pointX, &pointY, reinterpret_cast<DWRITE_HIT_TEST_METRICS*>(&hitTestMetrics)));
			return System::ValueTuple<Direct2D1::Point2F, HitTestMetrics>(Direct2D1::Point2F(pointX, pointY), hitTestMetrics);
		}
		bool HitTestTextRange(TextRange textRange, Direct2D1::Point2F origin, [System::Runtime::InteropServices::Out] HitTestMetrics% hitTestMetrics)
		{
			pin_ptr<HitTestMetrics> pHitTestMetrics = &hitTestMetrics;
			UINT32 actualHitTestMetricsCount;
			auto hr = GetPointer()->HitTestTextRange(safe_cast<UINT32>(textRange.StartPosition), safe_cast<UINT32>(textRange.Length), origin.X, origin.Y, reinterpret_cast<DWRITE_HIT_TEST_METRICS*>(pHitTestMetrics), 1, &actualHitTestMetricsCount);
			if (hr == E_NOT_SUFFICIENT_BUFFER)
				return false;
			ThrowIfFailed(hr);
			return true;
		}

		property float MaxWidth
		{
			float get() { return GetPointer()->GetMaxWidth(); }
			void set(float value) { ThrowIfFailed(GetPointer()->SetMaxWidth(value)); }
		}
		property TextMetrics Metrics
		{
			TextMetrics get()
			{
				TextMetrics textMetrics{};
				ThrowIfFailed(GetPointer()->GetMetrics(reinterpret_cast<DWRITE_TEXT_METRICS*>(&textMetrics)));
				return textMetrics;
			}
		}
		property TextAlignment TextAlignment
		{
			DirectWrite::TextAlignment get() { return safe_cast<DirectWrite::TextAlignment>(GetPointer()->GetTextAlignment()); }
			void set(DirectWrite::TextAlignment value) { ThrowIfFailed(GetPointer()->SetTextAlignment(safe_cast<DWRITE_TEXT_ALIGNMENT>(value))); }
		}

	internal:
		TextLayout() {}
		IDWriteTextLayout* GetPointer() { return static_cast<IDWriteTextLayout*>(p); }
	};
	
	public ref class TextLayout1 : public TextLayout
	{
	public:
		void SetCharacterSpacing(float leadingSpacing, float trailingSpacing, float minimumAdvanceWidth, TextRange textRange) { ThrowIfFailed(GetPointer()->SetCharacterSpacing(leadingSpacing, trailingSpacing, minimumAdvanceWidth, textRange.ToNative())); }

		static TextLayout1^ From(TextLayout^ textLayout)
		{
			auto textLayout1 = gcnew TextLayout1();
			PIN_LIGHT_COM_PTR_FOR_SET(textLayout1);
			textLayout->GetPointer()->QueryInterface(reinterpret_cast<IDWriteTextLayout1**>(ptextLayout1));
			return textLayout1;
		}

	internal:
		IDWriteTextLayout1* GetPointer() { return static_cast<IDWriteTextLayout1*>(p); }

	private:
		TextLayout1() {}
	};

	public ref class Factory : public Interop::DisposableComPtrBase<IDWriteFactory>
	{
	public:
		Factory()
		{
			PIN_LIGHT_COM_PTR_FOR_SET(this);
			ThrowIfFailed(DWriteCreateFactory(DWRITE_FACTORY_TYPE_SHARED, __uuidof(IDWriteFactory), reinterpret_cast<IUnknown**>(pthis)));
		}
		TextFormat^ CreateTextFormat(System::String^ fontFamilyName, int fontWeight, FontStyle fontStyle, int fontStretch, float fontSize)
		{
			TextFormat^ result = gcnew TextFormat();
			PIN_LIGHT_COM_PTR_FOR_SET(result);
			pin_ptr<const WCHAR> pFontFamilyName = PtrToStringChars(fontFamilyName);
			ThrowIfFailed(p->CreateTextFormat(pFontFamilyName, nullptr, safe_cast<DWRITE_FONT_WEIGHT>(fontWeight), safe_cast<DWRITE_FONT_STYLE>(fontStyle), safe_cast<DWRITE_FONT_STRETCH>(fontStretch), fontSize, L"", presult));
			return result;
		}
		TextLayout^ CreateTextLayout(System::String^ text, TextFormat^ textFormat, Direct2D1::SizeF maxSize)
		{
			pin_ptr<const WCHAR> pText = PtrToStringChars(text);
			TextLayout^ result = gcnew TextLayout();
			PIN_LIGHT_COM_PTR_FOR_SET(result);
			ThrowIfFailed(p->CreateTextLayout(pText, text->Length, textFormat->p, maxSize.Width, maxSize.Height, reinterpret_cast<IDWriteTextLayout**>(presult)));
			return result;
		}
	};
}
