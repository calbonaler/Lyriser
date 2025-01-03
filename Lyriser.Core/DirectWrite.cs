using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;
using Windows.Win32.Foundation;
using DWrite = Windows.Win32.Graphics.DirectWrite;
using PInvoke = Windows.Win32.PInvoke;

namespace Lyriser.Core.DirectWrite;

/// <summary>テキスト レイアウトに使用される測定方法です。</summary>
public enum MeasuringMode
{
	/// <summary>テキストは現在の表示解像度に依存しない値をもつグリフ理想的測定基準を使用して測定されます。</summary>
	Natural,
	/// <summary>テキストは現在の表示解像度に合わせて調整された値をもつグリフ表示互換測定基準を使用して測定されます。</summary>
	GdiClassic,
	/// <summary>
	/// テキストは CLEARTYPE_NATURAL_QUALITY で作成されたフォントを使用して
	/// GDI によって測定されるテキストと同じグリフ表示測定基準を使用して測定されます。
	/// </summary>
	GdiNatural
}

/// <summary>フォント フェイスのスタイルを通常、斜体、イタリック体で表します。</summary>
public enum FontStyle
{
	/// <summary>フォント スタイル: 通常</summary>
	Normal = 0,
	/// <summary>フォント スタイル: 斜体</summary>
	Oblique = 1,
	/// <summary>フォント スタイル: イタリック体</summary>
	Italic = 2,
}

/// <summary>特定の複数行段落で使用される単語の折り返し方法を指定します。</summary>
public enum WordWrapping
{
	/// <summary>割り付け矩形からはみ出さないように単語内で折り返します。</summary>
	Wrap = 0,
	/// <summary>
	/// 割り付け矩形からはみ出す場合でも同じ行に単語を維持します。
	/// このオプションははみ出したテキストを表示するためのスクロールとともによく使用されます。
	/// </summary>
	NoWrap = 1,
	/// <summary>
	/// 割り付け矩形からはみ出さないように単語内で折り返します。
	/// 単語が最大幅より大きい場合は緊急折り返しが発生します。
	/// </summary>
	[SupportedOSPlatform("windows8.1")]
	EmergencyBreak = 2,
	/// <summary>緊急折り返しの際、単語間での折り返しのみを行い、割り付け幅が1単語に対してすら小さすぎる場合でも単語内での折り返しは行いません。</summary>
	[SupportedOSPlatform("windows8.1")]
	WholeWord = 3,
	/// <summary>任意の有効な文字クラスタ間で折り返します。</summary>
	[SupportedOSPlatform("windows8.1")]
	Character = 4,
};

/// <summary>テキスト レイアウトの行送りの決定に使用される方法です。</summary>
public enum LineSpacingMethod
{
	/// <summary>行送りはコンテンツのみに依存し、フォント サイズやインライン オブジェクトに合わせて調整されます。</summary>
	Default = 0,
	/// <summary>
	/// フォント サイズやインライン オブジェクトに関係なく行送りは均等な値に明示的に設定されます。
	/// これはフォントのフォールバックによって発生する可能性のある不均一な外観を回避するのに役立ちます。
	/// </summary>
	Uniform = 1,
	/// <summary>行送りとベースラインの距離は、コンテンツ、フォント サイズ、およびインライン オブジェクトに基づいて計算された値に比例します。</summary>
	/// <remarks>この値はIDWriteTextLayout3::SetLineSpacingでのみ使用でき、IDWriteTextFormat::SetLineSpacingでは使用できません。</remarks>
	[SupportedOSPlatform("windows10.0")]
	Proportional = 2,
};

/// <summary>段落テキストの読字方向軸に沿った配置を割り付け矩形の始端と終端を基準として指定します。</summary>
public enum TextAlignment
{
	/// <summary>段落テキストの始端が割り付け矩形の始端に揃えられます。</summary>
	Leading = 0,
	/// <summary>段落テキストの終端が割り付け矩形の終端に揃えられます。</summary>
	Trailing = 1,
	/// <summary>段落テキストの中心が割り付け矩形の中心に揃えられます。</summary>
	Center = 2,
	/// <summary>テキストを始端に揃え、行を埋めるようにテキストを均等割り付けします。</summary>
	Justified = 3,
};

/// <summary>テキスト位置の範囲を指定します。</summary>
public record struct TextRange
{
	/// <summary>開始位置と長さからテキスト範囲を作成します。</summary>
	/// <param name="startPosition">開始位置を指定します。</param>
	/// <param name="length">長さを指定します。</param>
	/// <returns>開始位置と長さが設定されたテキスト範囲。</returns>
	public static TextRange FromStartLength(int startPosition, int length) => new() { StartPosition = startPosition, Length = length };
	/// <summary>開始位置と終了位置からテキスト範囲を作成します。</summary>
	/// <param name="startPosition">開始位置を指定します。</param>
	/// <param name="endPosition">終了位置を指定します。この位置は範囲に含まれません。</param>
	/// <returns>開始位置と終了位置が指定されたテキスト範囲。</returns>
	public static TextRange FromStartEnd(int startPosition, int endPosition) => FromStartLength(startPosition, endPosition - startPosition);

	/// <summary>テキスト範囲の終了位置です。この位置は範囲に含まれません。</summary>
	public readonly int EndPosition => StartPosition + Length;

	/// <summary>テキスト範囲の開始位置です。</summary>
	public int StartPosition;
	/// <summary>テキスト範囲の長さ（含まれる位置の数）です。</summary>
	public int Length;
};

/// <summary>グリフ クラスタに関する情報を格納します。</summary>
public record struct ClusterMetrics
{
	/// <summary>クラスタ内の全グリフの合計アドバンス幅です。</summary>
	public float Width;
	/// <summary>クラスタ内のテキスト位置の数です。</summary>
	public short Length;
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0044", Justification = "Assigned by native layer")]
	short bitValues;

	/// <summary>クラスタ直後で改行できるかどうかを示します。</summary>
	public readonly bool CanWrapLineAfter => (bitValues & 1) != 0;
	/// <summary>クラスタが空白文字に対応しているかどうかを示します。</summary>
	public readonly bool IsWhitespace => (bitValues & 2) != 0;
	/// <summary>クラスタが改行文字に対応しているかどうかを示します。</summary>
	public readonly bool IsNewline => (bitValues & 4) != 0;
	/// <summary>クラスタがソフトハイフン文字に対応しているかどうかを示します。</summary>
	public readonly bool IsSoftHyphen => (bitValues & 8) != 0;
	/// <summary>クラスタが右から左に読まれるかどうかを示します。</summary>
	public readonly bool IsRightToLeft => (bitValues & 16) != 0;
};

/// <summary>
/// 割り付け後のテキストに関連付けられた計量情報を格納します。
/// 座標の単位はデバイス独立ピクセル (DIP) です。
/// </summary>
public record struct TextMetrics
{
	/// <summary>グリフのオーバーハングを除いた、割り付け矩形を基準とした書式設定されたテキストの左上の点です。</summary>
	public System.Numerics.Vector2 TopLeft;
	/// <summary>各行末の空白を無視した書式設定されたテキストの幅です。</summary>
	public float Width;
	/// <summary>各行末の空白を考慮した書式設定されたテキストの幅です。</summary>
	public float WidthIncludingTrailingWhitespace;
	/// <summary>
	/// 書式設定されたテキストの高さです。
	/// 空文字列の高さは既定のフォントのそれと同じ値に設定されます。
	/// </summary>
	public float Height;
	/// <summary>
	/// レイアウトに与えられた大きさの初期値です。
	/// テキストの折り返しや長さによってテキスト内容の大きさと異なる値になる場合があります。
	/// </summary>
	public System.Numerics.Vector2 LayoutSize;
	/// <summary>
	/// 必要となるヒット テスト ボックスの最大数の計算に使用される任意のテキスト行の最大並び替え数です。
	/// レイアウトに双方向テキストがないかテキストが全くない場合、最小レベルは1です。
	/// </summary>
	public int MaxBidiReorderingDepth;
	/// <summary>総行数です。</summary>
	public int LineCount;
}

/// <summary>ヒット テストで得られた領域を記述します。</summary>
public record struct HitTestMetrics
{
	/// <summary>ヒット領域内のテキスト範囲です。</summary>
	public TextRange TextRange;
	/// <summary>ヒット領域の左上隅です。</summary>
	public System.Numerics.Vector2 TopLeft;
	/// <summary>ヒット領域の大きさです。</summary>
	public System.Numerics.Vector2 Size;
	/// <summary>ヒット領域内のテキスト位置の双方向レベルです。</summary>
	public int BidiLevel;
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0044", Justification = "Assigned by native layer")]
	BOOL _isText;
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0044", Justification = "Assigned by native layer")]
	BOOL _isTrimmed;

	/// <summary>ヒット領域にテキストがあるかどうかを示します。</summary>
	public readonly bool IsText => _isText;
	/// <summary>テキスト範囲がトリミングされているかどうかを示します。</summary>
	public readonly bool IsTrimmed => _isTrimmed;
	/// <summary>ヒット領域の右下隅です。</summary>
	public readonly System.Numerics.Vector2 BottomRight => TopLeft + Size;
};

/// <summary>複数行のテキスト段落に関する行送りの調整情報を表します。</summary>
/// <param name="LineSpacingMethod">行送りの決定方法を示す値です。</param>
/// <param name="LineSpacing">行送り、すなわち、ベースライン間の距離です。</param>
/// <param name="Baseline">
/// 行の編成方向における行の始端からベースラインまでの距離です。
/// <see cref="LineSpacing"/> に対する適切な比率は 80% です。
/// </param>
public record struct LineSpacingSet(LineSpacingMethod LineSpacingMethod, float LineSpacing, float Baseline);

/// <summary>
/// グリフ位置に対する省略可能な調整情報です。
/// グリフ オフセットはペンの位置に影響を与えることなくグリフの位置を変更します。
/// オフセットは変換前の論理単位で指定されます。
/// </summary>
public record struct GlyphOffset
{
	/// <summary>
	/// ランのアドバンス方向のオフセットです。
	/// 正の値を指定すると、ランが左から右の読字方向の場合は（変換前の座標において）右に、右から左の読字方向の場合は左に、グリフは移動します。
	/// </summary>
	public float AdvanceOffset;
	/// <summary>
	/// アセント方向、すなわち、アセンダーが指す方向へのオフセットです。
	/// 正の値を指定すると（変換前の座標において）グリフは上に移動します。
	/// </summary>
	public float AscenderOffset;
}

/// <summary>
/// レンダラーがグリフ ランを描画するのに必要な情報を格納します。
/// 座標の単位はデバイス独立ピクセル (DIP) です。
/// </summary>
public struct GlyphRun
{
#pragma warning disable CS0649
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0044", Justification = "Assigned by native layer")]
	unsafe void* _fontFace;
#pragma warning restore CS0649
	/// <summary>ポイントではなく（1/96 インチに等しい）DIP 単位のフォントの論理サイズです。</summary>
	public float FontEmSize;
#pragma warning disable CS0649
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0044", Justification = "Assigned by native layer")]
	int _glyphCount;
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0044", Justification = "Assigned by native layer")]
	unsafe ushort* _glyphIndices;
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0044", Justification = "Assigned by native layer")]
	unsafe float* _glyphAdvances;
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0044", Justification = "Assigned by native layer")]
	unsafe GlyphOffset* _glyphOffsets;
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0044", Justification = "Assigned by native layer")]
	BOOL _isSideways;
#pragma warning restore CS0649
	/// <summary>
	/// 暗黙的に解決されたランの双方向レベルです。
	/// 奇数レベルはヘブライ語やアラビア語のような右から左の読字方向の言語を、
	/// 偶数レベルは英語や（横書きの場合の）日本語のような左から右の読字方向の言語を示します。
	/// 右から左の読字方向の言語については、テキストの原点は右になり、左に向かって描画されます。
	/// </summary>
	public int BidiLevel;

	/// <summary>描画に使用する物理フォント フェイスを取得します。</summary>
	/// <returns>物理フォント フェイス。</returns>
	[SupportedOSPlatform("windows10.0.10240")]
	public readonly unsafe FontFace FetchFontFace() => new(_fontFace);

	/// <summary>レンダリングするグリフのインデックスを取得します。</summary>
	public readonly unsafe ReadOnlySpan<ushort> GlyphIndices => new(_glyphIndices, _glyphCount);
	/// <summary>グリフのアドバンス幅を取得します。</summary>
	public readonly unsafe ReadOnlySpan<float> GlyphAdvances => _glyphAdvances != null ? new(_glyphAdvances, _glyphCount) : default;
	/// <summary>グリフのオフセットを取得します。</summary>
	public readonly unsafe ReadOnlySpan<GlyphOffset> GlyphOffsets => _glyphOffsets != null ? new(_glyphOffsets, _glyphCount) : default;
	/// <summary>
	/// <see langword="true"/> の場合、グリフは 90 度左に回転され垂直メトリクスが使用されます。
	/// 縦書きは <see cref="IsSideways"/> = <see langword="true"/> とし、
	/// 回転変換によりラン全体を 90 度右に回転させることで達成されます。
	/// </summary>
	public readonly bool IsSideways => _isSideways;
}

/// <summary><see cref="GlyphRun"/> に関連した情報を格納します。</summary>
public struct GlyphRunDescription
{
#pragma warning disable CS0649
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0044", Justification = "Assigned by native layer")]
	unsafe char* _localeName;
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0044", Justification = "Assigned by native layer")]
	unsafe char* _string;
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0044", Justification = "Assigned by native layer")]
	int _stringLength;
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0044", Justification = "Assigned by native layer")]
	unsafe ushort* _clusterMap;
#pragma warning restore CS0649
	/// <summary>このグリフ ランの元となった文字列内の対応するテキスト位置です。</summary>
	public int TextPosition;

	/// <summary>このランに関連付けられたロケール名を取得します。</summary>
	public readonly unsafe string LocaleName => new(_localeName);
	/// <summary>グリフに関連付けられたテキストを取得します。</summary>
	public readonly unsafe ReadOnlySpan<char> String => new(_string, _stringLength);
	/// <summary>
	/// レンダリングするグリフのすべてのグリフ クラスタの最初のグリフの、グリフ
	/// インデックス配列へのインデックスの配列を取得します。
	/// </summary>
	public readonly unsafe ReadOnlySpan<ushort> ClusterMap => new(_clusterMap, _stringLength);
}

/// <summary>テキスト レンダラーのピクセル スナップに関する特性を定義します。</summary>
public interface IPixelSnapping
{
	/// <summary>
	/// ピクセル スナップが無効であるかどうかを示す値を取得します。
	/// 推奨される既定値は、サブピクセル垂直配置を必要とするアニメーションを実行しない限り <see langword="false"/> です。
	/// </summary>
	bool IsPixelSnappingDisabled { get; }
	/// <summary>
	/// 抽象座標を DIP にマッピングする現在の変換を取得します。
	/// これが回転または傾斜を含む場合ピクセル スナップを無効にする可能性があります。
	/// </summary>
	System.Numerics.Matrix3x2 CurrentTransform { get; }
	/// <summary>
	/// DIP ごとの物理ピクセル数を取得します。
	/// 1 DIP（デバイス独立ピクセル）は 1/96 インチであるので、この値はインチごとの論理ピクセル数を
	/// 96 で割った値（96 DPI では 1、120 DPI では 1.25）になります。
	/// </summary>
	float PixelsPerDip { get; }
}

/// <summary>テキスト、インライン オブジェクト、および、下線などの装飾のレンダリングを実行するアプリケーション定義のコールバック セットを表します。</summary>
public interface ITextRenderer : IPixelSnapping
{
	/// <summary>
	/// クライアントに一連のグリフをレンダリングするよう指示するために
	/// <see cref="TextLayout.Draw(nint, nint, System.Numerics.Vector2)"/> から呼び出されます。
	/// </summary>
	/// <param name="baselineOrigin">ベースライン座標です。</param>
	/// <param name="measuringMode">
	/// ラン内のグリフに対する測定モードを指定します。
	/// レンダラー実装は指定された測定モードについて他のレンダリング モードを選択できますが、
	/// レンダリング モードが対応する測定モードに一致している場合に最良の結果が得られます。
	/// <list type="bullet">
	/// <item><see cref="MeasuringMode.Natural"/> に対しては <c>DWRITE_RENDERING_MODE_NATURAL</c></item>
	/// <item><see cref="MeasuringMode.GdiClassic"/> に対しては <c>DWRITE_RENDERING_MODE_GDI_CLASSIC</c></item>
	/// <item><see cref="MeasuringMode.GdiNatural"/> に対しては <c>DWRITE_RENDERING_MODE_GDI_NATURAL</c></item>
	/// </list>
	/// </param>
	/// <param name="glyphRun">描画するグリフ ランです。</param>
	/// <param name="glyphRunDescription">このランに関連付けられた文字の情報です。</param>
	void DrawGlyphRun(System.Numerics.Vector2 baselineOrigin, MeasuringMode measuringMode, in GlyphRun glyphRun, in GlyphRunDescription glyphRunDescription);
}

unsafe struct NativeTextRendererImpl
{
	void** _vptr;
	uint _refCount;
	nint _managedPtr;

	static readonly Guid IDWriteTextRendererGuid = new("ef8a8135-5cc6-45fe-8825-c5a0724eb819");
	static readonly Guid IDWritePixelSnappingGuid = new("eaf3a2da-ecf4-4d24-b644-b34f6842024b");
	static readonly Guid IUnknownGuid = new("00000000-0000-0000-C000-000000000046");
	static readonly void** VTable;

	static NativeTextRendererImpl()
	{
		VTable = (void**)RuntimeHelpers.AllocateTypeAssociatedMemory(typeof(NativeTextRendererImpl), sizeof(void*) * (3 + 3 + 4));
		int idx = 0;
		// IUnknown
		VTable[idx++] = (delegate* unmanaged[Stdcall]<NativeTextRendererImpl*, Guid*, void**, HRESULT>)&QueryInterface;
		VTable[idx++] = (delegate* unmanaged[Stdcall]<NativeTextRendererImpl*, uint>)&AddRef;
		VTable[idx++] = (delegate* unmanaged[Stdcall]<NativeTextRendererImpl*, uint>)&Release;
		// IDWritePixelSnapping
		VTable[idx++] = (delegate* unmanaged[Stdcall]<NativeTextRendererImpl*, void*, BOOL*, HRESULT>)&IsPixelSnappingDisabled;
		VTable[idx++] = (delegate* unmanaged[Stdcall]<NativeTextRendererImpl*, void*, System.Numerics.Matrix3x2*, HRESULT>)&GetCurrentTransform;
		VTable[idx++] = (delegate* unmanaged[Stdcall]<NativeTextRendererImpl*, void*, float*, HRESULT>)&GetPixelsPerDip;
		// IDWriteTextRenderer
		VTable[idx++] = (delegate* unmanaged[Stdcall]<NativeTextRendererImpl*, void*, float, float, MeasuringMode, GlyphRun*, GlyphRunDescription*, void*, HRESULT>)&DrawGlyphRun;
		VTable[idx++] = (delegate* unmanaged[Stdcall]<NativeTextRendererImpl*, void*, float, float, void*, void*, HRESULT>)&DrawUnderline;
		VTable[idx++] = (delegate* unmanaged[Stdcall]<NativeTextRendererImpl*, void*, float, float, void*, void*, HRESULT>)&DrawStrikethrough;
		VTable[idx++] = (delegate* unmanaged[Stdcall]<NativeTextRendererImpl*, void*, float, float, void*, BOOL, BOOL, void*, HRESULT>)&DrawInlineObject;
	}

	internal static Interop.ComPtr<NativeTextRendererImpl> Create(ITextRenderer renderer)
	{
		Interop.ComPtr<NativeTextRendererImpl> comPtr;
		var ptr = (NativeTextRendererImpl*)NativeMemory.Alloc((nuint)sizeof(NativeTextRendererImpl));
		try
		{
			ptr->_vptr = VTable;
			ptr->_refCount = 1;
			ptr->_managedPtr = (nint)GCHandle.Alloc(renderer);
			comPtr = new Interop.ComPtr<NativeTextRendererImpl>();
		}
		catch
		{
			NativeMemory.Free(ptr);
			throw;
		}
		comPtr.Put() = ptr;
		return comPtr;
	}
	static ITextRenderer? ManagedObject(NativeTextRendererImpl* self) => (ITextRenderer?)((GCHandle)self->_managedPtr).Target;

	// IUnknown
	[UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
	static HRESULT QueryInterface(NativeTextRendererImpl* self, Guid* guid, void** ppv)
	{
		if (*guid == IDWriteTextRendererGuid || *guid == IDWritePixelSnappingGuid || *guid == IUnknownGuid)
		{
			((delegate* unmanaged[Stdcall]<void*, uint>)self->_vptr[1])(self); // AddRef
			*ppv = self;
			return HRESULT.S_OK;
		}
		*ppv = null;
		return HRESULT.E_NOINTERFACE;
	}
	[UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
	static uint AddRef(NativeTextRendererImpl* self) => Interlocked.Increment(ref self->_refCount);
	[UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
	static uint Release(NativeTextRendererImpl* self)
	{
		uint newRefCount = Interlocked.Decrement(ref self->_refCount);
		if (newRefCount == 0)
		{
			if (self->_managedPtr != nint.Zero)
			{
				((GCHandle)self->_managedPtr).Free();
				self->_managedPtr = nint.Zero;
			}
			NativeMemory.Free(self);
		}
		return newRefCount;
	}
	// IDWritePixelSnapping
	[UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
	static HRESULT IsPixelSnappingDisabled(NativeTextRendererImpl* self, void* clientDrawingContext, BOOL* isDisabled)
	{
		try
		{
			*isDisabled = ManagedObject(self)!.IsPixelSnappingDisabled;
			return HRESULT.S_OK;
		}
		catch (Exception ex)
		{
			Debug.Fail(ex.Message);
			return new HRESULT(ex.HResult);
		}
	}
	[UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
	static HRESULT GetCurrentTransform(NativeTextRendererImpl* self, void* clientDrawingContext, System.Numerics.Matrix3x2* transform)
	{
		try
		{
			*transform = ManagedObject(self)!.CurrentTransform;
			return HRESULT.S_OK;
		}
		catch (Exception ex)
		{
			Debug.Fail(ex.Message);
			return new HRESULT(ex.HResult);
		}
	}
	[UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
	static HRESULT GetPixelsPerDip(NativeTextRendererImpl* self, void* clientDrawingContext, float* pixelsPerDip)
	{
		try
		{
			*pixelsPerDip = ManagedObject(self)!.PixelsPerDip;
			return HRESULT.S_OK;
		}
		catch (Exception ex)
		{
			Debug.Fail(ex.Message);
			return new HRESULT(ex.HResult);
		}
	}
	// IDWriteTextRenderer
	[UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
	static HRESULT DrawGlyphRun(NativeTextRendererImpl* self, void* clientDrawingContext, float baselineOriginX, float baselineOriginY, MeasuringMode measuringMode, GlyphRun* glyphRun, GlyphRunDescription* glyphRunDescription, void* clientDrawingEffect)
	{
		try
		{
			ManagedObject(self)!.DrawGlyphRun(new(baselineOriginX, baselineOriginY), measuringMode, *glyphRun, *glyphRunDescription);
			return HRESULT.S_OK;
		}
		catch (Exception ex)
		{
			Debug.Fail(ex.Message);
			return new HRESULT(ex.HResult);
		}
	}
	[UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
	static HRESULT DrawUnderline(NativeTextRendererImpl* self, void* clientDrawingContext, float baselineOriginX, float baselineOriginY, void* underline, void* clientDrawingEffect) => HRESULT.E_NOTIMPL;
	[UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
	static HRESULT DrawStrikethrough(NativeTextRendererImpl* self, void* clientDrawingContext, float baselineOriginX, float baselineOriginY, void* strikethrough, void* clientDrawingEffect) => HRESULT.E_NOTIMPL;
	[UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
	static HRESULT DrawInlineObject(NativeTextRendererImpl* self, void* clientDrawingContext, float originX, float originY, void* inlineObject, BOOL isSideways, BOOL isRightToLeft, void* clientDrawingEffect) => HRESULT.E_NOTIMPL;
}

/// <summary>
/// メトリクス、名前、グリフ アウトラインなどのさまざまなフォント データを公開します。
/// フォント フェイスの種類、適切なファイル参照、および、フェイスの識別用データも含まれます。
/// </summary>
[SupportedOSPlatform("windows10.0.10240")]
public unsafe class FontFace : Interop.ComPtr
{
	internal FontFace(void* ptr) => Interop.ComUtils.Cast<DWrite.IDWriteFontFace3>(ptr, out PutIntPtr());

	/// <summary>フォントの太さを取得します。</summary>
	public int Weight => Pointer->GetWeight();
	/// <summary>フォントの幅の引き伸ばし度合いを取得します。</summary>
	public int Stretch => Pointer->GetStretch();
	/// <summary>フォントのスタイル（傾斜度）を取得します。</summary>
	public FontStyle Style => Pointer->GetStyle();
	/// <summary>
	/// ロケール名によって添え字付けされた太さ、幅、スタイルによるフォント ファミリ名のリストから最初のファミリ名を取得します。
	/// </summary>
	/// <returns>ロケール名によって添え字付けされたリスト内の最初のファミリ名。</returns>
	public string GetFirstFamilyName()
	{
		using var localizedStrings = new Interop.ComPtr<DWrite.IDWriteLocalizedStrings>();
		fixed (DWrite.IDWriteLocalizedStrings** p = &localizedStrings.Put())
			Pointer->GetFamilyNames(p);
		uint length;
		localizedStrings.Pointer->GetStringLength(0, &length);
		var buffer = new char[length + 1];
		fixed (char* p = buffer)
			localizedStrings.Pointer->GetString(0, p, length + 1);
		return buffer.AsSpan(..^1).ToString();
	}

	internal new DWrite.IDWriteFontFace3* Pointer => (DWrite.IDWriteFontFace3*)base.Pointer;
}

/// <summary>テキストの書式設定に使用されるフォントおよび段落に関するプロパティおよびロケール情報を記述します。</summary>
public unsafe class TextFormat : Interop.ComPtr
{
	internal TextFormat() { }

	/// <summary>複数行のテキスト段落に関する行送りの調整情報を取得または設定します。</summary>
	public LineSpacingSet LineSpacing
	{
		get
		{
			ThrowIfDisposed();
			var lineSpacingMethod = LineSpacingMethod.Default;
			var lineSpacing = 0f;
			var baseline = 0f;
			Pointer->GetLineSpacing(&lineSpacingMethod, &lineSpacing, &baseline);
			return new(lineSpacingMethod, lineSpacing, baseline);
		}
		set
		{
			ThrowIfDisposed();
			Pointer->SetLineSpacing(value.LineSpacingMethod, value.LineSpacing, value.Baseline);
		}
	}
	/// <summary>単語の折り返し方法を取得または設定します。</summary>
	public WordWrapping WordWrapping
	{
		get
		{
			ThrowIfDisposed();
			return Pointer->GetWordWrapping();
		}
		set
		{
			ThrowIfDisposed();
			Pointer->SetWordWrapping(value);
		}
	}
	/// <summary>割り付け矩形の始端および終端を基準としたテキストの配置方法を取得または設定します。</summary>
	public TextAlignment TextAlignment
	{
		get
		{
			ThrowIfDisposed();
			return Pointer->GetTextAlignment();
		}
		set
		{
			ThrowIfDisposed();
			Pointer->SetTextAlignment(value);
		}
	}

	internal new DWrite.IDWriteTextFormat* Pointer => (DWrite.IDWriteTextFormat*)base.Pointer;
	internal ref DWrite.IDWriteTextFormat* Put() => ref Interop.ComUtils.IntPtrAs<DWrite.IDWriteTextFormat>(ref PutIntPtr());
}

/// <summary>完全に分析および書式設定された後のテキスト ブロックを表します。</summary>
public unsafe class TextLayout : TextFormat
{
	internal TextLayout() { }

	/// <summary>テキストの描画を開始します。</summary>
	/// <param name="renderer">実際のレンダリングを行うアプリケーション定義のコールバック セットです。</param>
	/// <param name="origin">割り付け矩形の左上隅の座標です。</param>
	public void Draw(ITextRenderer renderer, System.Numerics.Vector2 origin)
	{
		using var ptr = NativeTextRendererImpl.Create(renderer);
		Pointer->Draw(null, ptr.Pointer, origin.X, origin.Y);
	}
	/// <summary>各グリフ クラスタの論理プロパティおよび測定情報を取得します。</summary>
	/// <returns>改行や合計アドバンス幅といったグリフ クラスタに関する計量情報。</returns>
	public ClusterMetrics[] GetClusterMetrics()
	{
		ThrowIfDisposed();
		var actualClusterCount = 0u;
		var hr = Pointer->GetClusterMetrics(null, 0, &actualClusterCount);
		if (hr != PInvoke.HRESULT_FROM_WIN32(WIN32_ERROR.ERROR_INSUFFICIENT_BUFFER))
			hr.ThrowOnFailure();
		if (actualClusterCount == 0) return [];
		var clusterMetrics = new ClusterMetrics[actualClusterCount];
		fixed (ClusterMetrics* ptr = &clusterMetrics[0])
			Pointer->GetClusterMetrics(ptr, actualClusterCount, &actualClusterCount).ThrowOnFailure();
		return clusterMetrics;
	}
	/// <summary>テキスト位置と位置の論理的な方向を指定して、割り付け矩形の左上を基準とした相対的なピクセル位置を取得します。</summary>
	/// <param name="textPosition">ピクセル位置を取得するために使用されるテキスト位置です。</param>
	/// <param name="isTrailingHit">ピクセル位置が指定されたテキスト位置の始端側にあるのか、あるいは終端側にあるのかを示します。</param>
	/// <returns>割り付け矩形の左上を基準としたピクセル位置、および指定されたテキスト位置を完全に囲む出力ジオメトリ。</returns>
	public (System.Numerics.Vector2 Point, HitTestMetrics HitTestMetrics) HitTestTextPosition(int textPosition, bool isTrailingHit)
	{
		ThrowIfDisposed();
		var hitTestMetrics = new HitTestMetrics();
		var pointX = 0f;
		var pointY = 0f;
		Pointer->HitTestTextPosition((uint)textPosition, isTrailingHit, &pointX, &pointY, &hitTestMetrics);
		return (new System.Numerics.Vector2(pointX, pointY), hitTestMetrics);
	}
	/// <summary>テキスト位置の範囲に対応するヒット テスト計量情報のセットを取得します。</summary>
	/// <param name="textRange">テキスト位置の範囲です。</param>
	/// <param name="origin">
	/// 割り付け矩形の左上の原点ピクセルの位置です。
	/// このオフセットは返されるヒット テスト計量情報に加算されます。
	/// </param>
	/// <param name="hitTestMetrics">
	/// 指定された位置範囲を完全に囲む出力ジオメトリのバッファです。
	/// このバッファの初期サイズとして適した値は <see cref="Metrics"/> で取得できる値を用いた次の式から計算できます。
	/// <code><see cref="TextMetrics.LineCount"/> * <see cref="TextMetrics.MaxBidiReorderingDepth"/></code>
	/// </param>
	/// <returns>
	/// <paramref name="hitTestMetrics"/> のバッファに実際に保持されるジオメトリの数。
	/// バッファが小さすぎて計算されたすべての領域を保持できない場合は -1。
	/// </returns>
	public int HitTestTextRange(TextRange textRange, System.Numerics.Vector2 origin, Span<HitTestMetrics> hitTestMetrics)
	{
		ThrowIfDisposed();
		HRESULT hr;
		var actualHitTestMetricsCount = 0u;
		fixed (HitTestMetrics* pHitTestMetrics = hitTestMetrics)
			hr = Pointer->HitTestTextRange((uint)textRange.StartPosition, (uint)textRange.Length, origin.X, origin.Y, pHitTestMetrics, (uint)hitTestMetrics.Length, &actualHitTestMetricsCount);
		if (hr == PInvoke.HRESULT_FROM_WIN32(WIN32_ERROR.ERROR_INSUFFICIENT_BUFFER))
			return -1;
		hr.ThrowOnFailure();
		return (int)actualHitTestMetricsCount;
	}
	/// <summary>テキスト位置の範囲に対応するヒット テスト計量情報を取得します。</summary>
	/// <param name="textRange">テキスト位置の範囲です。</param>
	/// <param name="origin">
	/// 割り付け矩形の左上の原点ピクセルの位置です。
	/// このオフセットは返されるヒット テスト計量情報に加算されます。
	/// </param>
	/// <param name="hitTestMetrics">指定された位置範囲を完全に囲む出力ジオメトリです。</param>
	/// <returns>ジオメトリが 1 つのために <paramref name="hitTestMetrics"/> にジオメトリが出力された場合は <see langword="true"/>。それ以外の場合は <see langword="false"/>。</returns>
	public bool HitTestTextRange(TextRange textRange, System.Numerics.Vector2 origin, out HitTestMetrics hitTestMetrics)
	{
		ThrowIfDisposed();
		Unsafe.SkipInit(out hitTestMetrics);
		return HitTestTextRange(textRange, origin, new(ref hitTestMetrics)) >= 0;
	}
	/// <summary>字間を設定します。</summary>
	/// <param name="leadingSpacing">読字方向において各文字の前にある間隔です。</param>
	/// <param name="trailingSpacing">読字方向において各文字の次にある間隔です。</param>
	/// <param name="minimumAdvanceWidth">
	/// 文字の幅が小さすぎたり 0 になったりするのを防ぐための各文字の最小アドバンス幅です。
	/// これは 0 以上でなければなりません。
	/// </param>
	/// <param name="textRange">この変更が適用されるテキスト範囲です。</param>
	[SupportedOSPlatform("windows8.0")]
	public void SetCharacterSpacing(float leadingSpacing, float trailingSpacing, float minimumAdvanceWidth, TextRange textRange)
	{
		ThrowIfDisposed();
		using var textLayout1 = Interop.ComUtils.Cast<DWrite.IDWriteTextLayout1>(Pointer);
		textLayout1.Pointer->SetCharacterSpacing(leadingSpacing, trailingSpacing, minimumAdvanceWidth, textRange);
	}

	/// <summary>割り付け矩形の最大幅を取得または設定します。</summary>
	public float MaxWidth
	{
		get
		{
			ThrowIfDisposed();
			return Pointer->GetMaxWidth();
		}
		set
		{
			ThrowIfDisposed();
			Pointer->SetMaxWidth(value);
		}
	}
	/// <summary>書式設定された文字列の全体的な計量情報を取得します。</summary>
	public TextMetrics Metrics
	{
		get
		{
			ThrowIfDisposed();
			var textMetrics = new TextMetrics();
			Pointer->GetMetrics(&textMetrics);
			return textMetrics;
		}
	}

	internal new DWrite.IDWriteTextLayout* Pointer => (DWrite.IDWriteTextLayout*)base.Pointer;
	internal new ref DWrite.IDWriteTextLayout* Put() => ref Interop.ComUtils.As<DWrite.IDWriteTextFormat, DWrite.IDWriteTextLayout>(ref base.Put());
}

/// <summary>すべての DirectWrite オブジェクトの作成に使用されるルート ファクトリです。</summary>
public unsafe class Factory : Interop.ComPtr
{
	/// <summary>個別の DirectWrite オブジェクトの後続の作成に使用される DirectWrite ファクトリ オブジェクトを作成します。</summary>
	/// <remarks>作成されるファクトリ オブジェクトは共有ファクトリ オブジェクトになります。</remarks>
	public Factory()
	{
		var guid = typeof(DWrite.IDWriteFactory).GUID;
		fixed (nint* pp = &PutIntPtr())
			PInvoke.DWriteCreateFactory(DWrite.DWRITE_FACTORY_TYPE.DWRITE_FACTORY_TYPE_SHARED, &guid, pp).ThrowOnFailure();
	}

	/// <summary>テキストの割り付けに使用されるテキスト書式設定オブジェクトを作成します。</summary>
	/// <param name="fontFamilyName">フォント ファミリの名前です。</param>
	/// <param name="fontWeight">このメソッドで作成されるテキスト オブジェクトのフォントの太さを表す1-999の値です。標準値は400です。</param>
	/// <param name="fontStyle">このメソッドで作成されるテキスト オブジェクトのフォント スタイルです。</param>
	/// <param name="fontStretch">このメソッドで作成されるテキスト オブジェクトのフォントの幅の引き伸ばし度合いを表す1-9の整数値です。標準値は5です。0は不明を意味します。</param>
	/// <param name="fontSize">DIP（デバイス独立ピクセル）単位で表されるフォントの論理サイズです。1 DIP は 1/96 インチです。</param>
	/// <returns>新しく作成されたテキスト書式設定オブジェクトです。</returns>
	public TextFormat CreateTextFormat(string fontFamilyName, int fontWeight, FontStyle fontStyle, int fontStretch, float fontSize)
	{
		ThrowIfDisposed();
		var emptyString = '\0';
		var result = new TextFormat();
		fixed (DWrite.IDWriteTextFormat** ppResult = &result.Put())
		fixed (char* pFontFamilyName = fontFamilyName)
			Pointer->CreateTextFormat(pFontFamilyName, null, fontWeight, fontStyle, fontStretch, fontSize, &emptyString, ppResult);
		return result;
	}
	/// <summary>文字列、テキスト書式設定および関連付けられた制約から完全に分析および書式設定された結果を表すオブジェクトを生成します。</summary>
	/// <param name="text">新しい <see cref="TextLayout"/> オブジェクトを作成する文字列です。</param>
	/// <param name="textFormat">文字列に適用される書式設定を示すオブジェクトです。</param>
	/// <param name="maxSize">割り付け矩形の大きさです。</param>
	/// <returns>結果のテキスト レイアウト オブジェクトです。</returns>
	public TextLayout CreateTextLayout(ReadOnlySpan<char> text, TextFormat textFormat, System.Numerics.Vector2 maxSize)
	{
		ThrowIfDisposed();
		ArgumentNullException.ThrowIfNull(textFormat);
		textFormat.ThrowIfDisposed();
		var result = new TextLayout();
		fixed (DWrite.IDWriteTextLayout** ppResult = &result.Put())
		fixed (char* pinnedText = text)
		{
			char emptyString = '\0';
			Pointer->CreateTextLayout(pinnedText != null ? pinnedText : &emptyString, (uint)text.Length, textFormat.Pointer, maxSize.X, maxSize.Y, ppResult);
		}
		return result;
	}

	internal new DWrite.IDWriteFactory* Pointer => (DWrite.IDWriteFactory*)base.Pointer;
};
