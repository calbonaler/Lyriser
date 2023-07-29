using System;
using System.Diagnostics.CodeAnalysis;
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

namespace Windows.Win32.Graphics.Direct2D
{
	[Guid("06152247-6F50-465A-9245-118BFD3B6007")]
	[SupportedOSPlatform("windows6.1")]
	unsafe struct ID2D1Factory
	{
		public void CreateDxgiSurfaceRenderTarget(Dxgi.IDXGISurface* dxgiSurface, D2D1_RENDER_TARGET_PROPERTIES* renderTargetProperties, ID2D1RenderTarget** renderTarget)
		{
			((delegate* unmanaged[Stdcall]<ID2D1Factory*, Dxgi.IDXGISurface*, D2D1_RENDER_TARGET_PROPERTIES*, ID2D1RenderTarget**, Foundation.HRESULT>)lpVtbl[15])((ID2D1Factory*)Unsafe.AsPointer(ref this), dxgiSurface, renderTargetProperties, renderTarget).ThrowOnFailure();
		}

#pragma warning disable CS0649
		void** lpVtbl;
#pragma warning restore CS0649
	}

	[Guid("2CD90694-12E2-11DC-9FED-001143A055F9")]
	[SupportedOSPlatform("windows6.1")]
	unsafe struct ID2D1RenderTarget
	{
		public void CreateSolidColorBrush(Lyriser.Core.Direct2D1.ColorF* color, void* brushProperties, ID2D1SolidColorBrush** solidColorBrush)
		{
			((delegate* unmanaged[Stdcall]<ID2D1RenderTarget*, Lyriser.Core.Direct2D1.ColorF*, void*, ID2D1SolidColorBrush**, Foundation.HRESULT>)lpVtbl[8])((ID2D1RenderTarget*)Unsafe.AsPointer(ref this), color, brushProperties, solidColorBrush).ThrowOnFailure();
		}

		public void FillRectangle(Lyriser.Core.Direct2D1.RectF* rect, ID2D1Brush* brush)
		{
			((delegate* unmanaged[Stdcall]<ID2D1RenderTarget*, Lyriser.Core.Direct2D1.RectF*, ID2D1Brush*, void>)lpVtbl[17])((ID2D1RenderTarget*)Unsafe.AsPointer(ref this), rect, brush);
		}

		public void DrawTextLayout(System.Numerics.Vector2 origin, DirectWrite.IDWriteTextLayout* textLayout, ID2D1Brush* defaultFillBrush, Lyriser.Core.Direct2D1.DrawTextOptions options)
		{
			((delegate* unmanaged[Stdcall]<ID2D1RenderTarget*, System.Numerics.Vector2, DirectWrite.IDWriteTextLayout*, ID2D1Brush*, Lyriser.Core.Direct2D1.DrawTextOptions, void>)lpVtbl[28])((ID2D1RenderTarget*)Unsafe.AsPointer(ref this), origin, textLayout, defaultFillBrush, options);
		}

		public void SetTransform(System.Numerics.Matrix3x2* transform)
		{
			((delegate* unmanaged[Stdcall]<ID2D1RenderTarget*, System.Numerics.Matrix3x2*, void>)lpVtbl[30])((ID2D1RenderTarget*)Unsafe.AsPointer(ref this), transform);
		}

		public void GetTransform(System.Numerics.Matrix3x2* transform)
		{
			((delegate* unmanaged[Stdcall]<ID2D1RenderTarget*, System.Numerics.Matrix3x2*, void>)lpVtbl[31])((ID2D1RenderTarget*)Unsafe.AsPointer(ref this), transform);
		}

		public void PushAxisAlignedClip(Lyriser.Core.Direct2D1.RectF* clipRect, Lyriser.Core.Direct2D1.AntialiasMode antialiasMode)
		{
			((delegate* unmanaged[Stdcall]<ID2D1RenderTarget*, Lyriser.Core.Direct2D1.RectF*, Lyriser.Core.Direct2D1.AntialiasMode, void>)lpVtbl[45])((ID2D1RenderTarget*)Unsafe.AsPointer(ref this), clipRect, antialiasMode);
		}

		public void PopAxisAlignedClip()
		{
			((delegate* unmanaged[Stdcall]<ID2D1RenderTarget*, void>)lpVtbl[46])((ID2D1RenderTarget*)Unsafe.AsPointer(ref this));
		}

		public void Clear(Lyriser.Core.Direct2D1.ColorF* clearColor)
		{
			((delegate* unmanaged[Stdcall]<ID2D1RenderTarget*, Lyriser.Core.Direct2D1.ColorF*, void>)lpVtbl[47])((ID2D1RenderTarget*)Unsafe.AsPointer(ref this), clearColor);
		}

		public void BeginDraw()
		{
			((delegate* unmanaged[Stdcall]<ID2D1RenderTarget*, void>)lpVtbl[48])((ID2D1RenderTarget*)Unsafe.AsPointer(ref this));
		}

		public void EndDraw(ulong* tag1, ulong* tag2)
		{
			((delegate* unmanaged[Stdcall]<ID2D1RenderTarget*, ulong*, ulong*, Foundation.HRESULT>)lpVtbl[49])((ID2D1RenderTarget*)Unsafe.AsPointer(ref this), tag1, tag2).ThrowOnFailure();
		}

		public System.Numerics.Vector2 GetSize()
		{
			return ((delegate* unmanaged[Stdcall, MemberFunction]<ID2D1RenderTarget*, System.Numerics.Vector2>)lpVtbl[53])((ID2D1RenderTarget*)Unsafe.AsPointer(ref this));
		}

#pragma warning disable CS0649
		void** lpVtbl;
#pragma warning restore CS0649
	}

	[Guid("2CD906A8-12E2-11DC-9FED-001143A055F9")]
	[SupportedOSPlatform("windows6.1")]
	unsafe struct ID2D1Brush
	{
		[SuppressMessage("Style", "IDE0044", Justification = "Reserved member for COM vtable")]
		[SuppressMessage("CodeQuality", "IDE0051", Justification = "Reserved member for COM vtable")]
#pragma warning disable CS0649
#pragma warning disable CS0169
		void** lpVtbl;
#pragma warning restore CS0169
#pragma warning restore CS0649
	}

	[Guid("2CD906A9-12E2-11DC-9FED-001143A055F9")]
	[SupportedOSPlatform("windows6.1")]
	unsafe struct ID2D1SolidColorBrush
	{
		[SuppressMessage("Style", "IDE0044", Justification = "Reserved member for COM vtable")]
		[SuppressMessage("CodeQuality", "IDE0051", Justification = "Reserved member for COM vtable")]
#pragma warning disable CS0649
#pragma warning disable CS0169
		void** lpVtbl;
#pragma warning restore CS0169
#pragma warning restore CS0649
	}
}

namespace Windows.Win32.Graphics.Direct3D9
{
	[Guid("02177241-69FC-400C-8FF1-93A44DF6861D")]
	unsafe struct IDirect3D9Ex
	{
		public void CreateDeviceEx(uint Adapter, D3DDEVTYPE DeviceType, Foundation.HWND hFocusWindow, uint BehaviorFlags, D3DPRESENT_PARAMETERS* pPresentationParameters, void* pFullscreenDisplayMode, IDirect3DDevice9Ex** ppReturnedDeviceInterface)
		{
			((delegate* unmanaged[Stdcall]<IDirect3D9Ex*, uint, D3DDEVTYPE, Foundation.HWND, uint, D3DPRESENT_PARAMETERS*, void*, IDirect3DDevice9Ex**, Foundation.HRESULT>)lpVtbl[20])((IDirect3D9Ex*)Unsafe.AsPointer(ref this), Adapter, DeviceType, hFocusWindow, BehaviorFlags, pPresentationParameters, pFullscreenDisplayMode, ppReturnedDeviceInterface).ThrowOnFailure();
		}

#pragma warning disable CS0649
		void** lpVtbl;
#pragma warning restore CS0649
	}

	[Guid("B18B10CE-2649-405A-870F-95F777D4313A")]
	unsafe struct IDirect3DDevice9Ex
	{
		public void CreateRenderTarget(uint Width,uint Height, D3DFORMAT Format, D3DMULTISAMPLE_TYPE MultiSample, uint MultisampleQuality, Foundation.BOOL Lockable, IDirect3DSurface9** ppSurface, void** pSharedHandle)
		{
			((delegate* unmanaged[Stdcall]<IDirect3DDevice9Ex*, uint, uint, D3DFORMAT, D3DMULTISAMPLE_TYPE, uint, Foundation.BOOL, IDirect3DSurface9**, void**, Foundation.HRESULT>)lpVtbl[28])((IDirect3DDevice9Ex*)Unsafe.AsPointer(ref this), Width, Height, Format, MultiSample, MultisampleQuality, Lockable, ppSurface, pSharedHandle).ThrowOnFailure();
		}

#pragma warning disable CS0649
		void** lpVtbl;
#pragma warning restore CS0649
	}

	[Guid("0CFBAF3A-9FF6-429a-99B3-A2796AF8B89B")]
	unsafe struct IDirect3DSurface9
	{
		[SuppressMessage("Style", "IDE0044", Justification = "Reserved member for COM vtable")]
		[SuppressMessage("CodeQuality", "IDE0051", Justification = "Reserved member for COM vtable")]
#pragma warning disable CS0649
#pragma warning disable CS0169
		void** lpVtbl;
#pragma warning restore CS0169
#pragma warning restore CS0649
	}
}

namespace Windows.Win32.Graphics.Direct3D11
{
	[Guid("DB6F6DDB-AC77-4E88-8253-819DF9BBF140")]
	[SupportedOSPlatform("windows6.1")]
	unsafe struct ID3D11Device
	{
		public void OpenSharedResource(void* hResource, Guid* ReturnedInterface, nint* ppResource)
		{
			((delegate* unmanaged[Stdcall]<ID3D11Device*, void*, Guid*, nint*, Foundation.HRESULT>)lpVtbl[28])((ID3D11Device*)Unsafe.AsPointer(ref this), hResource, ReturnedInterface, ppResource).ThrowOnFailure();
		}

#pragma warning disable CS0649
		void** lpVtbl;
#pragma warning restore CS0649
	}

	[Guid("C0BFA96C-E089-44FB-8EAF-26F8796190DA")]
	[SupportedOSPlatform("windows6.1")]
	unsafe struct ID3D11DeviceContext
	{
		public void Flush()
		{
			((delegate* unmanaged[Stdcall]<ID3D11DeviceContext*, void>)lpVtbl[111])((ID3D11DeviceContext*)Unsafe.AsPointer(ref this));
		}

#pragma warning disable CS0649
		void** lpVtbl;
#pragma warning restore CS0649
	}
}

namespace Windows.Win32.Graphics.Dxgi
{
	[Guid("CAFCB56C-6AC3-4889-BF47-9E23BBD260EC")]
	unsafe struct IDXGISurface
	{
		[SuppressMessage("Style", "IDE0044", Justification = "Reserved member for COM vtable")]
		[SuppressMessage("CodeQuality", "IDE0051", Justification = "Reserved member for COM vtable")]
#pragma warning disable CS0649
#pragma warning disable CS0169
		void** lpVtbl;
#pragma warning restore CS0169
#pragma warning restore CS0649
	}
}

namespace Windows.Win32
{
	static partial class PInvoke
	{
		[DllImport("d3d11.dll", ExactSpelling = true)]
		[DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
		internal static extern unsafe Foundation.HRESULT D3D11CreateDevice(void* pAdapter, Graphics.Direct3D.D3D_DRIVER_TYPE DriverType, void* Software, Graphics.Direct3D11.D3D11_CREATE_DEVICE_FLAG Flags, void* pFeatureLevels, uint FeatureLevels, uint SDKVersion, Graphics.Direct3D11.ID3D11Device** ppDevice, void* pFeatureLevel, Graphics.Direct3D11.ID3D11DeviceContext** ppImmediateContext);

		[DllImport("d2d1.dll", ExactSpelling = true)]
		[DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
		[SupportedOSPlatform("windows6.1")]
		internal static extern unsafe Foundation.HRESULT D2D1CreateFactory(Graphics.Direct2D.D2D1_FACTORY_TYPE factoryType, Guid* riid,  void* pFactoryOptions, nint* ppIFactory);

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