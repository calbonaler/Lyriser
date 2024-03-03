using System;
using PInvoke = Windows.Win32.PInvoke;
using D2D = Windows.Win32.Graphics.Direct2D;
using System.Text;

namespace Lyriser.Core.Direct2D1;

/// <summary>色の赤、緑、青、アルファの各成分を記述します。</summary>
public record struct ColorF
{
	/// <summary>赤、緑、青およびアルファの成分値から <see cref="ColorF"/> 構造体を作成します。</summary>
	/// <param name="red">赤。有効な値は 0 から 1 です。</param>
	/// <param name="green">緑。有効な値は 0 から 1 です。</param>
	/// <param name="blue">青。有効な値は 0 から 1 です。</param>
	/// <param name="alpha">アルファ。有効な値は 0 から 1 です。</param>
	public ColorF(float red, float green, float blue, float alpha)
	{
		Red = red;
		Green = green;
		Blue = blue;
		Alpha = alpha;
	}
	/// <summary>
	/// 赤、緑、および青の成分値から <see cref="ColorF"/> 構造体を作成します。
	/// アルファ値は 1 が使用されます。
	/// </summary>
	/// <param name="red">赤。有効な値は 0 から 1 です。</param>
	/// <param name="green">緑。有効な値は 0 から 1 です。</param>
	/// <param name="blue">青。有効な値は 0 から 1 です。</param>
	public ColorF(float red, float green, float blue) : this(red, green, blue, 1) { }
	/// <summary>24 ビットの RGB 値およびアルファの成分値から <see cref="ColorF"/> 構造体を作成します。</summary>
	/// <param name="rgb">24 ビットの RGB 値。</param>
	/// <param name="alpha">アルファ。有効な値は 0 から 1 です。</param>
	/// <remarks>
	/// 24ビット RGB 値のバイト順は RRGGBB です。
	/// 最上位バイトは未使用です。
	/// RR、GG、BBで表される各バイトはそれぞれ赤、緑、青の色成分です。
	/// </remarks>
	public ColorF(int rgb, float alpha) : this(((rgb >> 16) & 0xff) / 255.0f, ((rgb >> 8) & 0xff) / 255.0f, (rgb & 0xff) / 255.0f, alpha) { }
	/// <summary>
	/// 24 ビットの RGB 値から <see cref="ColorF"/> 構造体を作成します。
	/// アルファ値は 1 が使用されます。
	/// </summary>
	/// <param name="rgb">24 ビットの RGB 値。</param>
	/// <remarks>
	/// 24ビット RGB 値のバイト順は RRGGBB です。
	/// 最上位バイトは未使用です。
	/// RR、GG、BBで表される各バイトはそれぞれ赤、緑、青の色成分です。
	/// </remarks>
	public ColorF(int rgb) : this(rgb, 1) { }

	/// <summary>色の赤成分値。有効な値は 0 から 1 です。</summary>
	public float Red;
	/// <summary>色の緑成分値。有効な値は 0 から 1 です。</summary>
	public float Green;
	/// <summary>色の青成分値。有効な値は 0 から 1 です。</summary>
	public float Blue;
	/// <summary>色のアルファ成分値。有効な値は 0 から 1 です。</summary>
	public float Alpha;
};

/// <summary>左上隅および右下隅の2点によって定義された矩形を表します。</summary>
public record struct RectF
{
	/// <summary>左上隅および右下隅の2点から <see cref="RectF"/> 構造体を作成します。</summary>
	/// <param name="topLeft">矩形の左上隅を表す <see cref="System.Numerics.Vector2"/>。</param>
	/// <param name="bottomRight">矩形の右下隅を表す <see cref="System.Numerics.Vector2"/>。</param>
	/// <returns>新しい <see cref="RectF"/> オブジェクト。</returns>
	public static RectF FromLTRB(System.Numerics.Vector2 topLeft, System.Numerics.Vector2 bottomRight) => new() { TopLeft = topLeft, BottomRight = bottomRight };
	/// <summary>左上隅および右下隅の2点から <see cref="RectF"/> 構造体を作成します。</summary>
	/// <param name="left">矩形の左上隅の X 座標。</param>
	/// <param name="top">矩形の左上隅の Y 座標。</param>
	/// <param name="right">矩形の右下隅の X 座標。</param>
	/// <param name="bottom">矩形の右下隅の Y 座標。</param>
	/// <returns>新しい <see cref="RectF"/> オブジェクト。</returns>
	public static RectF FromLTRB(float left, float top, float right, float bottom) => FromLTRB(new(left, top), new(right, bottom));
	/// <summary>位置および大きさから <see cref="RectF"/> 構造体を作成します。</summary>
	/// <param name="location">矩形の左上隅を表す <see cref="System.Numerics.Vector2"/>。</param>
	/// <param name="size">矩形の大きさを表す <see cref="System.Numerics.Vector2"/>。</param>
	/// <returns>新しい <see cref="RectF"/> オブジェクト。</returns>
	public static RectF FromXYWH(System.Numerics.Vector2 location, System.Numerics.Vector2 size) => FromLTRB(location, location + size);
	/// <summary>位置および大きさから <see cref="RectF"/> 構造体を作成します。</summary>
	/// <param name="x">矩形の左上隅の X 座標。</param>
	/// <param name="y">矩形の左上隅の Y 座標。</param>
	/// <param name="width">矩形の幅。</param>
	/// <param name="height">矩形の高さ。</param>
	/// <returns>新しい <see cref="RectF"/> オブジェクト。</returns>
	public static RectF FromXYWH(float x, float y, float width, float height) => FromXYWH(new(x, y), new(width, height));

	/// <summary>矩形の左上隅を表す <see cref="System.Numerics.Vector2"/>。</summary>
	public System.Numerics.Vector2 TopLeft;
	/// <summary>矩形の右下隅を表す <see cref="System.Numerics.Vector2"/>。</summary>
	public System.Numerics.Vector2 BottomRight;
	/// <summary>
	/// 矩形の大きさを表す <see cref="System.Numerics.Vector2"/>。
	/// 設定した場合、<see cref="TopLeft"/> は維持され <see cref="BottomRight"/> が変更されます。
	/// </summary>
	public System.Numerics.Vector2 Size
	{
		readonly get => BottomRight - TopLeft;
		set => BottomRight = TopLeft + value;
	}
};

/// <summary>非テキスト プリミティブのエッジをレンダリングする方法を指定します。</summary>
public enum AntialiasMode : uint
{
	/// <summary>エッジは Direct2D のプリミティブごとの高品質アンチエイリアス手法を使用してアンチエイリアス処理されます。</summary>
	PerPrimitive = 0,
	/// <summary>
	/// ほとんどの場合オブジェクトはエイリアス表示されます。
	/// CreateDxgiSurfaceRenderTarget で作成されたレンダー ターゲットに描画され、かつ、基になる DirectX Graphics Infrastructure (DXGI)
	/// サーフェイスでマルチサンプリングが有効化されている場合のみ、オブジェクトはアンチエイリアス処理されます。
	/// </summary>
	Aliased = 1,
}

/// <summary>
/// テキストのスナップを抑制するか、および、割り付け矩形へのクリッピングを有効化するかを指定します。
/// この列挙体はメンバ値のビットごとの組み合わせが可能です。
/// </summary>
[Flags]
public enum DrawTextOptions : uint
{
	/// <summary>テキストはピクセル境界に垂直スナップされ、割り付け矩形でクリップされません。</summary>
	None = 0x00000000,
	/// <summary>
	/// テキストはピクセル境界に垂直スナップされません。
	/// この設定はアニメーションされるテキストに推奨されます。
	/// </summary>
	NoSnap = 0x00000001,
	/// <summary>テキストは割り付け矩形でクリップされます。</summary>
	Clip = 0x00000002,
	/// <summary>Windows 8.1 以上で、フォントで定義されている場合、テキストは色付き版のグリフでレンダリングされます。</summary>
	EnableColorFont = 0x00000004,
	/// <summary>色付きグリフ ビットマップのビットマップ原点はスナップされません。</summary>
	DisableColorBitmapSnapping = 0x00000008,
};

/// <summary>Direct2D リソースを作成します。</summary>
public sealed unsafe class Factory : Interop.ComPtr
{
	internal Factory()
	{
		var guid = typeof(D2D.ID2D1Factory).GUID;
		fixed (nint* pp = &PutIntPtr())
			PInvoke.D2D1CreateFactory(D2D.D2D1_FACTORY_TYPE.D2D1_FACTORY_TYPE_SINGLE_THREADED, &guid, null, pp).ThrowOnFailure();
	}

	internal new D2D.ID2D1Factory* Pointer => (D2D.ID2D1Factory*)base.Pointer;
}

/// <summary>
/// 領域を塗りつぶすオブジェクトを定義します。
/// 派生クラスは領域を塗りつぶす方法を記述します。
/// </summary>
public abstract unsafe class Brush : Interop.ComPtr
{
	internal new D2D.ID2D1Brush* Pointer => (D2D.ID2D1Brush*)base.Pointer;
}

/// <summary>領域を単色で塗りつぶします。</summary>
public unsafe class SolidColorBrush : Brush
{
	internal SolidColorBrush() { }
	internal new D2D.ID2D1SolidColorBrush* Pointer => (D2D.ID2D1SolidColorBrush*)base.Pointer;
	internal ref D2D.ID2D1SolidColorBrush* Put() => ref Interop.ComUtils.IntPtrAs<D2D.ID2D1SolidColorBrush>(ref PutIntPtr());
}

/// <summary>
/// 描画コマンドを受け付けるオブジェクトを表します。
/// 派生クラスは受け付けた描画コマンドをさまざまな方法でレンダリングします。
/// </summary>
public unsafe class RenderTarget
{
	internal RenderTarget(Interop.ComPtr<D2D.ID2D1RenderTarget> comPtr) => ComPtr = comPtr;

	/// <summary>指定された色の新しい <see cref="SolidColorBrush"/> を作成します。</summary>
	/// <param name="color">ブラシの色の赤、緑、青およびアルファ値です。</param>
	/// <returns>新しく作成されたブラシ。</returns>
	public SolidColorBrush CreateSolidColorBrush(ColorF color)
	{
		ThrowIfDisposed();
		var result = new SolidColorBrush();
		fixed (D2D.ID2D1SolidColorBrush** p = &result.Put())
			ComPtr.Pointer->CreateSolidColorBrush(&color, null, p);
		return result;
	}

	/// <summary>描画領域を指定された色でクリアします。</summary>
	/// <param name="color">描画領域をクリアする色を指定します。</param>
	/// <remarks>レンダー ターゲットに（<see cref="PushAxisAlignedClip(RectF, AntialiasMode)"/>で指定される）アクティブなクリップがある場合、クリア コマンドはクリップ領域内にのみ適用されます。</remarks>
	public void Clear(ColorF color)
	{
		ThrowIfDisposed();
		ComPtr.Pointer->Clear(&color);
	}
	/// <summary>指定された <see cref="DirectWrite.TextLayout"/> オブジェクトによって記述される書式設定されたテキストを描画します。</summary>
	/// <param name="origin"><paramref name="textLayout"/> によって記述されるテキストの左上隅が描画されるデバイス独立ピクセル単位で記述される点です。</param>
	/// <param name="textLayout">描画する書式設定されたテキストです。</param>
	/// <param name="defaultBrush"><paramref name="textLayout"/> 内の任意のテキストの塗りつぶしに使用されるブラシです。</param>
	/// <param name="options">
	/// テキストをピクセル境界にスナップするべきか、および、テキストを割り付け矩形でクリップすべきかを示す値です。
	/// 既定値は <see cref="DrawTextOptions.None"/> で、テキストをピクセル境界にスナップし割り付け矩形でクリップしません。
	/// </param>
	public void DrawTextLayout(System.Numerics.Vector2 origin, DirectWrite.TextLayout textLayout, Brush defaultBrush, DrawTextOptions options = DrawTextOptions.None)
	{
		ThrowIfDisposed();
		ArgumentNullException.ThrowIfNull(textLayout);
		ArgumentNullException.ThrowIfNull(defaultBrush);
		textLayout.ThrowIfDisposed();
		defaultBrush.ThrowIfDisposed();
		ComPtr.Pointer->DrawTextLayout(origin, textLayout.Pointer, defaultBrush.Pointer, options);
	}
	/// <summary>指定された矩形の内部を塗りつぶします。</summary>
	/// <param name="rect">デバイス独立ピクセルで記述された塗りつぶし対象の矩形です。</param>
	/// <param name="brush">矩形内部の塗りつぶしに使用されるブラシです。</param>
	public void FillRectangle(RectF rect, Brush brush)
	{
		ThrowIfDisposed();
		ArgumentNullException.ThrowIfNull(brush);
		brush.ThrowIfDisposed();
		ComPtr.Pointer->FillRectangle(&rect, brush.Pointer);
	}
	/// <summary>以降のすべての描画操作がクリップされる矩形を設定します。</summary>
	/// <param name="clipRect">デバイス独立ピクセルで記述されたクリッピング領域の位置と大きさです。</param>
	/// <param name="antialiasMode">
	/// サブピクセル境界のあるクリップ矩形のエッジの描画およびクリップのシーン内容とのブレンドに使用されるアンチエイリアス モードです。
	/// ブレンド処理は <see cref="PopAxisAlignedClip"/> の呼び出し時に 1 回実行され、レイヤ内の個々のプリミティブには適用されません。
	/// </param>
	/// <remarks>
	/// <para>
	/// <paramref name="clipRect"/> にはレンダー ターゲットの現在のワールド変換が適用されます。
	/// ただし、クリップ処理は効率のために、変換された <paramref name="clipRect"/> ではなくその軸整列境界ボックスで行われます。
	/// </para>
	/// <para><see cref="PushAxisAlignedClip(RectF, AntialiasMode)"/> と <see cref="PopAxisAlignedClip"/> は一致しなければなりません。</para>
	/// </remarks>
	public void PushAxisAlignedClip(RectF clipRect, AntialiasMode antialiasMode)
	{
		ThrowIfDisposed();
		ComPtr.Pointer->PushAxisAlignedClip(&clipRect, antialiasMode);
	}
	/// <summary>
	/// 最後に設定された軸整列クリップをレンダー ターゲットから取り除きます。
	/// このメソッドの呼び出し以降、クリップは描画操作に適用されなくなります。
	/// </summary>
	/// <remarks>このメソッドは <see cref="PushAxisAlignedClip(RectF, AntialiasMode)"/> の各呼び出しに対して 1 回呼び出されなければなりません。</remarks>
	public void PopAxisAlignedClip()
	{
		ThrowIfDisposed();
		ComPtr.Pointer->PopAxisAlignedClip();
	}
	/// <summary>レンダー ターゲットの大きさをデバイス独立ピクセルで取得します。</summary>
	public System.Numerics.Vector2 Size
	{
		get
		{
			ThrowIfDisposed();
			return ComPtr.Pointer->GetSize();
		}
	}
	/// <summary>
	/// レンダー ターゲットの現在の変換を取得または設定します。
	/// 設定すると以降のすべての描画操作が変換された空間で実行されます。
	/// </summary>
	public System.Numerics.Matrix3x2 Transform
	{
		get
		{
			ThrowIfDisposed();
			var result = new System.Numerics.Matrix3x2();
			ComPtr.Pointer->GetTransform(&result);
			return result;
		}
		set
		{
			ThrowIfDisposed();
			ComPtr.Pointer->SetTransform(&value);
		}
	}

	void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(ComPtr.IsNull, this);

	internal Interop.ComPtr<D2D.ID2D1RenderTarget> ComPtr { get; }
}
