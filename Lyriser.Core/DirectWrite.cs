using System;
using System.Runtime.Versioning;
using Windows.Win32.Foundation;
using PInvoke = Windows.Win32.PInvoke;
using DWrite = Windows.Win32.Graphics.DirectWrite;
using System.Runtime.CompilerServices;

namespace Lyriser.Core.DirectWrite;

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
	int isText;
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0044", Justification = "Assigned by native layer")]
	int isTrimmed;

	/// <summary>ヒット領域にテキストがあるかどうかを示します。</summary>
	public readonly bool IsText => isText != 0;
	/// <summary>テキスト範囲がトリミングされているかどうかを示します。</summary>
	public readonly bool IsTrimmed => isTrimmed != 0;
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
