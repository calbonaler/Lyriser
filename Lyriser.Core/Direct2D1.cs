using System;
using PInvoke = Windows.Win32.PInvoke;
using IUnknown = Windows.Win32.System.Com.IUnknown;
using D2D = Windows.Win32.Graphics.Direct2D;

namespace Lyriser.Core.Direct2D1;

public record struct ColorF
{
	public ColorF(float red, float green, float blue, float alpha)
	{
		Red = red;
		Green = green;
		Blue = blue;
		Alpha = alpha;
	}
	public ColorF(float red, float green, float blue) : this(red, green, blue, 1) { }
	public ColorF(int rgb, float alpha) : this(((rgb >> 16) & 0xff) / 255.0f, ((rgb >> 8) & 0xff) / 255.0f, (rgb & 0xff) / 255.0f, alpha) { }
	public ColorF(int rgb) : this(rgb, 1) { }

	public float Red;
	public float Green;
	public float Blue;
	public float Alpha;
};

public record struct RectF
{
	public static RectF FromLTRB(System.Numerics.Vector2 topLeft, System.Numerics.Vector2 bottomRight) => new() { TopLeft = topLeft, BottomRight = bottomRight };
	public static RectF FromLTRB(float left, float top, float right, float bottom) => FromLTRB(new(left, top), new(right, bottom));
	public static RectF FromXYWH(System.Numerics.Vector2 location, System.Numerics.Vector2 size) => FromLTRB(location, location + size);
	public static RectF FromXYWH(float x, float y, float width, float height) => FromXYWH(new(x, y), new(width, height));

	public System.Numerics.Vector2 TopLeft;
	public System.Numerics.Vector2 BottomRight;
	public System.Numerics.Vector2 Size
	{
		readonly get => BottomRight - TopLeft;
		set => BottomRight = TopLeft + value;
	}
};

public enum AntialiasMode : uint
{
	/// <summary>The edges of each primitive are antialiased sequentially.</summary>
	PerPrimitive = D2D.D2D1_ANTIALIAS_MODE.D2D1_ANTIALIAS_MODE_PER_PRIMITIVE,
	/// <summary>Each pixel is rendered if its pixel center is contained by the geometry.</summary>
	Aliased = D2D.D2D1_ANTIALIAS_MODE.D2D1_ANTIALIAS_MODE_ALIASED,
}

[Flags]
public enum DrawTextOptions : uint
{
	None = D2D.D2D1_DRAW_TEXT_OPTIONS.D2D1_DRAW_TEXT_OPTIONS_NONE,
	/// <summary>Do not snap the baseline of the text vertically.</summary>
	NoSnap = D2D.D2D1_DRAW_TEXT_OPTIONS.D2D1_DRAW_TEXT_OPTIONS_NO_SNAP,
	/// <summary>Clip the text to the content bounds.</summary>
	Clip = D2D.D2D1_DRAW_TEXT_OPTIONS.D2D1_DRAW_TEXT_OPTIONS_CLIP,
	/// <summary>Render color versions of glyphs if defined by the font.</summary>
	EnableColorFont = D2D.D2D1_DRAW_TEXT_OPTIONS.D2D1_DRAW_TEXT_OPTIONS_ENABLE_COLOR_FONT,
	/// <summary>Bitmap origins of color glyph bitmaps are not snapped.</summary>
	DisableColorBitmapSnapping = D2D.D2D1_DRAW_TEXT_OPTIONS.D2D1_DRAW_TEXT_OPTIONS_DISABLE_COLOR_BITMAP_SNAPPING,
};

public sealed unsafe class Factory : Interop.DisposableComPtrBase
{
	public Factory() =>
		PInvoke.D2D1CreateFactory(D2D.D2D1_FACTORY_TYPE.D2D1_FACTORY_TYPE_SINGLE_THREADED, typeof(D2D.ID2D1Factory).GUID, null, out PutVoid()).ThrowOnFailure();

	internal new D2D.ID2D1Factory* Pointer => (D2D.ID2D1Factory*)base.Pointer;
}

public abstract unsafe class Brush : Interop.DisposableComPtrBase
{
	internal new D2D.ID2D1Brush* Pointer => (D2D.ID2D1Brush*)base.Pointer;
}

public unsafe class SolidColorBrush : Brush
{
	internal SolidColorBrush() { }
	internal new D2D.ID2D1SolidColorBrush* Pointer => (D2D.ID2D1SolidColorBrush*)base.Pointer;
	internal new ref D2D.ID2D1SolidColorBrush* Put() => ref Interop.ComUtils.As<IUnknown, D2D.ID2D1SolidColorBrush>(ref base.Put());
}
public abstract unsafe class RenderTarget : Interop.ComPtrBase
{
	public SolidColorBrush CreateSolidColorBrush(ColorF color)
	{
		var result = new SolidColorBrush();
		fixed (D2D.ID2D1SolidColorBrush** p = &result.Put())
			Pointer->CreateSolidColorBrush((D2D.Common.D2D1_COLOR_F*)&color, null, p);
		return result;
	}

	public void Clear(ColorF color) => Pointer->Clear((D2D.Common.D2D1_COLOR_F*)&color);
	public void DrawTextLayout(System.Numerics.Vector2 origin, DirectWrite.TextLayout textLayout, Brush defaultBrush, DrawTextOptions options) =>
		Pointer->DrawTextLayout(new() { x = origin.X, y = origin.Y }, textLayout.Pointer, defaultBrush.Pointer, (D2D.D2D1_DRAW_TEXT_OPTIONS)options);
	public void DrawTextLayout(System.Numerics.Vector2 origin, DirectWrite.TextLayout textLayout, Brush defaultBrush) =>
		DrawTextLayout(origin, textLayout, defaultBrush, DrawTextOptions.None);
	public void FillRectangle(RectF rect, Brush brush) => Pointer->FillRectangle((D2D.Common.D2D_RECT_F*)&rect, brush.Pointer);
	public void PushAxisAlignedClip(RectF clipRect, AntialiasMode antialiasMode) =>
		Pointer->PushAxisAlignedClip((D2D.Common.D2D_RECT_F*)&clipRect, (D2D.D2D1_ANTIALIAS_MODE)antialiasMode);
	public void PopAxisAlignedClip() => Pointer->PopAxisAlignedClip();
	public System.Numerics.Vector2 Size
	{
		get
		{
			var size = ((delegate *unmanaged [Stdcall, MemberFunction]<D2D.ID2D1RenderTarget*, D2D.Common.D2D_SIZE_F>)VTable[53])(Pointer);
			return new System.Numerics.Vector2(size.width, size.height);
		}
	}
	public System.Numerics.Matrix3x2 Transform
	{
		get
		{
			var result = new System.Numerics.Matrix3x2();
			Pointer->GetTransform((D2D.Common.D2D_MATRIX_3X2_F*)&result);
			return result;
		}
		set => Pointer->SetTransform((D2D.Common.D2D_MATRIX_3X2_F*)&value);
	}

	nint* VTable => *(nint**)Pointer;
	internal new D2D.ID2D1RenderTarget* Pointer => (D2D.ID2D1RenderTarget*)base.Pointer;
	internal new ref D2D.ID2D1RenderTarget* Put() => ref Interop.ComUtils.As<IUnknown, D2D.ID2D1RenderTarget>(ref base.Put());
}

class RenderTargetImpl : RenderTarget, IDisposable
{
	internal RenderTargetImpl() { }
	~RenderTargetImpl() => Dispose(false);
	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}
	protected virtual void Dispose(bool disposing) { Release(); }
};
