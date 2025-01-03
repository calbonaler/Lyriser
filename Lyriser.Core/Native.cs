using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Windows.Win32.Graphics.DirectWrite
{
	[Guid("B859EE5A-D838-4B5B-A2E8-1ADC7D93DB48")]
	[SupportedOSPlatform("windows6.1")]
	unsafe struct IDWriteFactory
	{
		public void CreateTextFormat(Foundation.PCWSTR fontFamilyName, void* fontCollection, int fontWeight, Lyriser.Core.DirectWrite.FontStyle fontStyle, int fontStretch, float fontSize, Foundation.PCWSTR localeName, IDWriteTextFormat** textFormat)
		{
			((delegate* unmanaged[Stdcall]<IDWriteFactory*, Foundation.PCWSTR, void*, int, Lyriser.Core.DirectWrite.FontStyle, int, float, Foundation.PCWSTR, IDWriteTextFormat**, Foundation.HRESULT>)lpVtbl[15])((IDWriteFactory*)Unsafe.AsPointer(ref this), fontFamilyName, fontCollection, fontWeight, fontStyle, fontStretch, fontSize, localeName, textFormat).ThrowOnFailure();
		}

		public void CreateTextLayout(Foundation.PCWSTR @string, uint stringLength, IDWriteTextFormat* textFormat, float maxWidth, float maxHeight, IDWriteTextLayout** textLayout)
		{
			((delegate* unmanaged[Stdcall]<IDWriteFactory*, Foundation.PCWSTR, uint, IDWriteTextFormat*, float, float, IDWriteTextLayout**, Foundation.HRESULT>)lpVtbl[18])((IDWriteFactory*)Unsafe.AsPointer(ref this), @string, stringLength, textFormat, maxWidth, maxHeight, textLayout).ThrowOnFailure();
		}

#pragma warning disable CS0649
		void** lpVtbl;
#pragma warning restore CS0649
	}

	[Guid("08256209-099A-4B34-B86D-C22B110E7771")]
	[SupportedOSPlatform("windows6.1")]
	unsafe struct IDWriteLocalizedStrings
	{
		public void GetStringLength(uint index, uint* length)
		{
			((delegate* unmanaged[Stdcall]<IDWriteLocalizedStrings*, uint, uint*, Foundation.HRESULT>)lpVtbl[7])((IDWriteLocalizedStrings*)Unsafe.AsPointer(ref this), index, length).ThrowOnFailure();
		}

		public void GetString(uint index, Foundation.PWSTR stringBuffer, uint size)
		{
			((delegate* unmanaged[Stdcall]<IDWriteLocalizedStrings*, uint, Foundation.PWSTR, uint, Foundation.HRESULT>)lpVtbl[8])((IDWriteLocalizedStrings*)Unsafe.AsPointer(ref this), index, stringBuffer, size).ThrowOnFailure();
		}

#pragma warning disable CS0649
		void** lpVtbl;
#pragma warning restore CS0649
	}

	[Guid("D37D7598-09BE-4222-A236-2081341CC1F2")]
	[SupportedOSPlatform("windows10.0.10240")]
	unsafe struct IDWriteFontFace3
	{
		public int GetWeight()
		{
			return ((delegate* unmanaged[Stdcall]<IDWriteFontFace3*, int>)lpVtbl[37])((IDWriteFontFace3*)Unsafe.AsPointer(ref this));
		}

		public int GetStretch()
		{
			return ((delegate* unmanaged[Stdcall]<IDWriteFontFace3*, int>)lpVtbl[38])((IDWriteFontFace3*)Unsafe.AsPointer(ref this));
		}

		public Lyriser.Core.DirectWrite.FontStyle GetStyle()
		{
			return ((delegate* unmanaged[Stdcall]<IDWriteFontFace3*, Lyriser.Core.DirectWrite.FontStyle>)lpVtbl[39])((IDWriteFontFace3*)Unsafe.AsPointer(ref this));
		}

		public void GetFamilyNames(IDWriteLocalizedStrings** names)
		{
			((delegate* unmanaged[Stdcall]<IDWriteFontFace3*, IDWriteLocalizedStrings**, Foundation.HRESULT>)lpVtbl[40])((IDWriteFontFace3*)Unsafe.AsPointer(ref this), names).ThrowOnFailure();
		}

#pragma warning disable CS0649
		void** lpVtbl;
#pragma warning restore CS0649
	}

	[Guid("9C906818-31D7-4FD3-A151-7C5E225DB55A")]
	[SupportedOSPlatform("windows6.1")]
	unsafe struct IDWriteTextFormat
	{
		public void SetTextAlignment(Lyriser.Core.DirectWrite.TextAlignment textAlignment)
		{
			((delegate* unmanaged[Stdcall]<IDWriteTextLayout*, Lyriser.Core.DirectWrite.TextAlignment, Foundation.HRESULT>)lpVtbl[3])((IDWriteTextLayout*)Unsafe.AsPointer(ref this), textAlignment).ThrowOnFailure();
		}

		public void SetWordWrapping(Lyriser.Core.DirectWrite.WordWrapping wordWrapping)
		{
			((delegate* unmanaged[Stdcall]<IDWriteTextFormat*, Lyriser.Core.DirectWrite.WordWrapping, Foundation.HRESULT>)lpVtbl[5])((IDWriteTextFormat*)Unsafe.AsPointer(ref this), wordWrapping).ThrowOnFailure();
		}

		public void SetLineSpacing(Lyriser.Core.DirectWrite.LineSpacingMethod lineSpacingMethod, float lineSpacing, float baseline)
		{
			((delegate* unmanaged[Stdcall]<IDWriteTextFormat*, Lyriser.Core.DirectWrite.LineSpacingMethod, float, float, Foundation.HRESULT>)lpVtbl[10])((IDWriteTextFormat*)Unsafe.AsPointer(ref this), lineSpacingMethod, lineSpacing, baseline).ThrowOnFailure();
		}

		public Lyriser.Core.DirectWrite.TextAlignment GetTextAlignment()
		{
			return ((delegate* unmanaged[Stdcall]<IDWriteTextLayout*, Lyriser.Core.DirectWrite.TextAlignment>)lpVtbl[11])((IDWriteTextLayout*)Unsafe.AsPointer(ref this));
		}

		public Lyriser.Core.DirectWrite.WordWrapping GetWordWrapping()
		{
			return ((delegate* unmanaged[Stdcall]<IDWriteTextFormat*, Lyriser.Core.DirectWrite.WordWrapping>)lpVtbl[13])((IDWriteTextFormat*)Unsafe.AsPointer(ref this));
		}

		public void GetLineSpacing(Lyriser.Core.DirectWrite.LineSpacingMethod* lineSpacingMethod, float* lineSpacing, float* baseline)
		{
			((delegate* unmanaged[Stdcall]<IDWriteTextFormat*, Lyriser.Core.DirectWrite.LineSpacingMethod*, float*, float*, Foundation.HRESULT>)lpVtbl[18])((IDWriteTextFormat*)Unsafe.AsPointer(ref this), lineSpacingMethod, lineSpacing, baseline).ThrowOnFailure();
		}

#pragma warning disable CS0649
		void** lpVtbl;
#pragma warning restore CS0649
	}

	[Guid("53737037-6D14-410B-9BFE-0B182BB70961")]
	[SupportedOSPlatform("windows6.1")]
	unsafe struct IDWriteTextLayout
	{
		public void SetMaxWidth(float maxWidth)
		{
			((delegate* unmanaged[Stdcall]<IDWriteTextLayout*, float, Foundation.HRESULT>)lpVtbl[28])((IDWriteTextLayout*)Unsafe.AsPointer(ref this), maxWidth).ThrowOnFailure();
		}

		public float GetMaxWidth()
		{
			return ((delegate* unmanaged[Stdcall]<IDWriteTextLayout*, float>)lpVtbl[42])((IDWriteTextLayout*)Unsafe.AsPointer(ref this));
		}

		public void Draw(void* clientDrawingContext, void* renderer, float originX, float originY)
		{
			((delegate* unmanaged[Stdcall]<IDWriteTextLayout*, void*, void*, float, float, Foundation.HRESULT>)lpVtbl[58])((IDWriteTextLayout*)Unsafe.AsPointer(ref this), clientDrawingContext, renderer, originX, originY).ThrowOnFailure();
		}

		public void GetMetrics(Lyriser.Core.DirectWrite.TextMetrics* textMetrics)
		{
			((delegate* unmanaged[Stdcall]<IDWriteTextLayout*, Lyriser.Core.DirectWrite.TextMetrics*, Foundation.HRESULT>)lpVtbl[60])((IDWriteTextLayout*)Unsafe.AsPointer(ref this), textMetrics).ThrowOnFailure();
		}

		public Foundation.HRESULT GetClusterMetrics(Lyriser.Core.DirectWrite.ClusterMetrics* clusterMetrics, uint maxClusterCount, uint* actualClusterCount)
		{
			return ((delegate* unmanaged[Stdcall]<IDWriteTextLayout*, Lyriser.Core.DirectWrite.ClusterMetrics*, uint, uint*, Foundation.HRESULT>)lpVtbl[62])((IDWriteTextLayout*)Unsafe.AsPointer(ref this), clusterMetrics, maxClusterCount, actualClusterCount);
		}

		public void HitTestTextPosition(uint textPosition, Foundation.BOOL isTrailingHit, float* pointX, float* pointY, Lyriser.Core.DirectWrite.HitTestMetrics* hitTestMetrics)
		{
			((delegate* unmanaged[Stdcall]<IDWriteTextLayout*, uint, Foundation.BOOL, float*, float*, Lyriser.Core.DirectWrite.HitTestMetrics*, Foundation.HRESULT>)lpVtbl[65])((IDWriteTextLayout*)Unsafe.AsPointer(ref this), textPosition, isTrailingHit, pointX, pointY, hitTestMetrics).ThrowOnFailure();
		}

		public Foundation.HRESULT HitTestTextRange(uint textPosition, uint textLength, float originX, float originY, Lyriser.Core.DirectWrite.HitTestMetrics* hitTestMetrics, uint maxHitTestMetricsCount, uint* actualHitTestMetricsCount)
		{
			return ((delegate* unmanaged[Stdcall]<IDWriteTextLayout*, uint, uint, float, float, Lyriser.Core.DirectWrite.HitTestMetrics*, uint, uint*, Foundation.HRESULT>)lpVtbl[66])((IDWriteTextLayout*)Unsafe.AsPointer(ref this), textPosition, textLength, originX, originY, hitTestMetrics, maxHitTestMetricsCount, actualHitTestMetricsCount);
		}

#pragma warning disable CS0649
		void** lpVtbl;
#pragma warning restore CS0649
	}

	[Guid("9064D822-80A7-465C-A986-DF65F78B8FEB")]
	[SupportedOSPlatform("windows8.0")]
	unsafe struct IDWriteTextLayout1
	{
		public void SetCharacterSpacing(float leadingSpacing, float trailingSpacing, float minimumAdvanceWidth, Lyriser.Core.DirectWrite.TextRange textRange)
		{
			((delegate* unmanaged[Stdcall]<IDWriteTextLayout1*, float, float, float, Lyriser.Core.DirectWrite.TextRange, Foundation.HRESULT>)lpVtbl[69])((IDWriteTextLayout1*)Unsafe.AsPointer(ref this), leadingSpacing, trailingSpacing, minimumAdvanceWidth, textRange).ThrowOnFailure();
		}

#pragma warning disable CS0649
		void** lpVtbl;
#pragma warning restore CS0649
	}
}

namespace Windows.Win32
{
	static partial class PInvoke
	{
		[DllImport("DWrite.dll", ExactSpelling = true)]
		[DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
		[SupportedOSPlatform("windows6.1")]
		internal static extern unsafe Foundation.HRESULT DWriteCreateFactory(Graphics.DirectWrite.DWRITE_FACTORY_TYPE factoryType, Guid* iid, nint* factory);
	}
}

namespace Windows.Win32.UI.Input.Ime
{
	[Guid("019F7152-E6DB-11D0-83C3-00C04FDDB82E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown), ComImport()]
	interface IFELanguage
	{
		[PreserveSig()]
		Foundation.HRESULT Open();

		[PreserveSig()]
		Foundation.HRESULT Close();

		[PreserveSig()]
		unsafe Foundation.HRESULT GetJMorphResult(uint dwRequest, uint dwCMode, int cwchInput, Foundation.PCWSTR pwchInput, uint* pfCInfo, MORRSLT** ppResult);
	}
}