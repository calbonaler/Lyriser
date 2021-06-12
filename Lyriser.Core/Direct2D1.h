#pragma once

#include "Common.h"
#include "Interop.h"
#include "DirectWrite.h"

namespace Lyriser::Core::Direct2D1
{
	public enum class AntialiasMode
	{
		/// <summary>The edges of each primitive are antialiased sequentially.</summary>
		PerPrimitive = D2D1_ANTIALIAS_MODE_PER_PRIMITIVE,
		/// <summary>Each pixel is rendered if its pixel center is contained by the geometry.</summary>
		Aliased = D2D1_ANTIALIAS_MODE_ALIASED,
	};

	[System::Flags]
	public enum class DrawTextOptions
	{
		None = D2D1_DRAW_TEXT_OPTIONS_NONE,
		/// <summary>Do not snap the baseline of the text vertically.</summary>
		NoSnap = D2D1_DRAW_TEXT_OPTIONS_NO_SNAP,
		/// <summary>Clip the text to the content bounds.</summary>
		Clip = D2D1_DRAW_TEXT_OPTIONS_CLIP,
		/// <summary>Render color versions of glyphs if defined by the font.</summary>
		EnableColorFont = D2D1_DRAW_TEXT_OPTIONS_ENABLE_COLOR_FONT,
		/// <summary>Bitmap origins of color glyph bitmaps are not snapped.</summary>
		DisableColorBitmapSnapping = D2D1_DRAW_TEXT_OPTIONS_DISABLE_COLOR_BITMAP_SNAPPING,
	};

	public ref class Factory : public Interop::DisposableComPtrBase<ID2D1Factory>
	{
	public:
		Factory()
		{
			PIN_LIGHT_COM_PTR_FOR_SET(this);
			ThrowIfFailed(D2D1CreateFactory(D2D1_FACTORY_TYPE_SINGLE_THREADED, pthis));
		}
	};

	public ref class Brush abstract : public Interop::DisposableComPtrBase<ID2D1Brush>
	{
	};

	public ref class SolidColorBrush : public Brush
	{
	internal:
		SolidColorBrush() { }
	};

	public ref class RenderTarget abstract : public Interop::ComPtrBase<ID2D1RenderTarget>
	{
	public:
		SolidColorBrush^ CreateSolidColorBrush(ColorF color)
		{
			SolidColorBrush^ result = gcnew SolidColorBrush();
			pin_ptr<decltype(result->p)> pinnedResult = &result->p;
			ID2D1SolidColorBrush** presult = reinterpret_cast<ID2D1SolidColorBrush**>(pinnedResult);
			ThrowIfFailed(p->CreateSolidColorBrush(reinterpret_cast<D2D1_COLOR_F*>(&color), nullptr, presult));
			return result;
		}

		void Clear(ColorF color) { p->Clear(reinterpret_cast<D2D1_COLOR_F*>(&color)); }
		void DrawTextLayout(System::Numerics::Vector2 origin, DirectWrite::TextLayout^ textLayout, Brush^ defaultBrush, DrawTextOptions options) { p->DrawTextLayout({ origin.X, origin.Y }, textLayout->GetPointer(), defaultBrush->p, safe_cast<D2D1_DRAW_TEXT_OPTIONS>(options)); }
		void DrawTextLayout(System::Numerics::Vector2 origin, DirectWrite::TextLayout^ textLayout, Brush^ defaultBrush) { DrawTextLayout(origin, textLayout, defaultBrush, DrawTextOptions::None); }
		void FillRectangle(RectF rect, Brush^ brush) { p->FillRectangle(reinterpret_cast<D2D1_RECT_F*>(&rect), brush->p); }
		void PushAxisAlignedClip(RectF clipRect, AntialiasMode antialiasMode) { p->PushAxisAlignedClip(reinterpret_cast<D2D1_RECT_F*>(&clipRect), safe_cast<D2D1_ANTIALIAS_MODE>(antialiasMode)); }
		void PopAxisAlignedClip() { p->PopAxisAlignedClip(); }
		property System::Numerics::Vector2 Size
		{
			System::Numerics::Vector2 get()
			{
				auto size = p->GetSize();
				return System::Numerics::Vector2(size.width, size.height);
			}
		}
		property System::Numerics::Matrix3x2 Transform
		{
			System::Numerics::Matrix3x2 get()
			{
				System::Numerics::Matrix3x2 result{};
				p->GetTransform(reinterpret_cast<D2D1_MATRIX_3X2_F*>(&result));
				return result;
			}
			void set(System::Numerics::Matrix3x2 value) { p->SetTransform(reinterpret_cast<D2D1_MATRIX_3X2_F*>(&value)); }
		}

	};

	ref class RenderTargetImpl : public RenderTarget
	{
	public:
		~RenderTargetImpl() { this->!RenderTargetImpl(); }
		!RenderTargetImpl() { Release(); }

	internal:
		RenderTargetImpl() { }
	};
}
