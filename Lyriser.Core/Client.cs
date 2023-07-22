using System;
using Windows.Win32.Foundation;
using PInvoke = Windows.Win32.PInvoke;
using D2D = Windows.Win32.Graphics.Direct2D;
using D3D = Windows.Win32.Graphics.Direct3D;
using D3D9 = Windows.Win32.Graphics.Direct3D9;
using D3D11 = Windows.Win32.Graphics.Direct3D11;
using Dxgi = Windows.Win32.Graphics.Dxgi;

namespace Lyriser.Core;

class D3D9InteropClient : IDisposable
{
	public D3D9InteropClient(Action<nint> backBufferSetter)
	{
		ArgumentNullException.ThrowIfNull(backBufferSetter);
		lock (s_LockObject)
		{
			if (s_ActiveClients++ == 0)
				StartD3D();
		}
		m_BackBufferSetter = backBufferSetter;
	}
	public unsafe void Dispose()
	{
		SetRenderTarget(null);
		lock (s_LockObject)
		{
			if (--s_ActiveClients == 0)
				EndD3D();
		}
	}
	public unsafe void SetRenderTarget(D3D11.ID3D11Texture2D* d3D11RenderTarget)
	{
		if (m_D3D9RenderTarget.Pointer != null)
		{
			m_BackBufferSetter(0);
			m_D3D9RenderTarget.Release();
		}
		if (d3D11RenderTarget == null)
			return;
		var format = TranslateFormat(d3D11RenderTarget);
		var handle = GetSharedHandle(d3D11RenderTarget);
		if (!IsShareable(d3D11RenderTarget))
			throw new ArgumentException("Texture must be created with ResouceOptionFlags.Shared", nameof(d3D11RenderTarget));
		if (format == D3D9.D3DFORMAT.D3DFMT_UNKNOWN)
			throw new ArgumentException("Texture format is not compatible with OpenSharedResouce", nameof(d3D11RenderTarget));
		if (handle == HANDLE.Null)
			throw new ArgumentException("Invalid handle", nameof(d3D11RenderTarget));
		{
			d3D11RenderTarget->GetDesc(out var desc);
			fixed (D3D9.IDirect3DTexture9** pp = &m_D3D9RenderTarget.Put())
				s_D3D9Device.Pointer->CreateTexture(desc.Width, desc.Height, 1, PInvoke.D3DUSAGE_RENDERTARGET, format, D3D9.D3DPOOL.D3DPOOL_DEFAULT, pp, &handle);
		}
		var ptr = nint.Zero;
		using Interop.ComPtr<D3D9.IDirect3DSurface9> surface = new Interop.ComPtr<D3D9.IDirect3DSurface9>();
		fixed (D3D9.IDirect3DSurface9** pp = &surface.Put())
			m_D3D9RenderTarget.Pointer->GetSurfaceLevel(0, pp);
		m_BackBufferSetter((nint)surface.Pointer);
	}
	public bool IsRenderTargetValid => m_D3D9RenderTarget != null;

	static unsafe void StartD3D()
	{
		fixed (D3D9.IDirect3D9Ex** pp = &s_D3D9Context.Put())
			PInvoke.Direct3DCreate9Ex(PInvoke.D3D_SDK_VERSION, pp).ThrowOnFailure();
		var presentParams = new D3D9.D3DPRESENT_PARAMETERS
		{
			Windowed = true,
			SwapEffect = D3D9.D3DSWAPEFFECT.D3DSWAPEFFECT_DISCARD,
			hDeviceWindow = PInvoke.GetDesktopWindow(),
			PresentationInterval = PInvoke.D3DPRESENT_INTERVAL_DEFAULT
		};
		fixed (D3D9.IDirect3DDevice9Ex** pp = &s_D3D9Device.Put())
			s_D3D9Context.Pointer->CreateDeviceEx(0, D3D9.D3DDEVTYPE.D3DDEVTYPE_HAL, HWND.Null,
				PInvoke.D3DCREATE_HARDWARE_VERTEXPROCESSING | PInvoke.D3DCREATE_MULTITHREADED | PInvoke.D3DCREATE_FPU_PRESERVE,
				&presentParams, null, pp);
	}
	static void EndD3D()
	{
		s_D3D9Device.Release();
		s_D3D9Context.Release();
	}
	static unsafe HANDLE GetSharedHandle(D3D11.ID3D11Texture2D* texture)
	{
		using var resource = Interop.ComUtils.Cast<Dxgi.IDXGIResource>(texture);
		HANDLE sharedHandle;
		resource.Pointer->GetSharedHandle(&sharedHandle);
		return sharedHandle;
	}
	static unsafe D3D9.D3DFORMAT TranslateFormat(D3D11.ID3D11Texture2D* texture)
	{
		texture->GetDesc(out var desc);
		return desc.Format switch
		{
			Dxgi.Common.DXGI_FORMAT.DXGI_FORMAT_R10G10B10A2_UNORM => D3D9.D3DFORMAT.D3DFMT_A2B10G10R10,
			Dxgi.Common.DXGI_FORMAT.DXGI_FORMAT_R16G16B16A16_FLOAT => D3D9.D3DFORMAT.D3DFMT_A16B16G16R16F,
			Dxgi.Common.DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM => D3D9.D3DFORMAT.D3DFMT_A8R8G8B8,
			_ => D3D9.D3DFORMAT.D3DFMT_UNKNOWN,
		};
	}
	static unsafe bool IsShareable(D3D11.ID3D11Texture2D* texture)
	{
		texture->GetDesc(out var desc);
		return (desc.MiscFlags & D3D11.D3D11_RESOURCE_MISC_FLAG.D3D11_RESOURCE_MISC_SHARED) != 0;
	}

	static readonly object s_LockObject = new();
	static readonly Interop.ComPtr<D3D9.IDirect3D9Ex> s_D3D9Context = new();
	static readonly Interop.ComPtr<D3D9.IDirect3DDevice9Ex> s_D3D9Device = new();
	static int s_ActiveClients;

	readonly Interop.ComPtr<D3D9.IDirect3DTexture9> m_D3D9RenderTarget = new();
	readonly Action<nint> m_BackBufferSetter;
}

public sealed class D2D3D9InteropClient : IDisposable
{
	// - field -----------------------------------------------------------------------
	readonly D3D9InteropClient m_InteropClient;
	readonly Interop.ComPtr<D3D11.ID3D11Device> m_D3D11Device = new();
	readonly Interop.ComPtr<D3D11.ID3D11DeviceContext> m_D3D11Context = new();
	readonly Interop.ComPtr<D3D11.ID3D11Texture2D> m_D3D11RenderTarget = new();
	readonly Direct2D1.RenderTargetImpl m_D2D1RenderTarget = new();

	readonly Action<Direct2D1.RenderTarget> m_Renderer;
	readonly Action<Direct2D1.RenderTarget> m_ResourcesUpdater;

	// - public methods --------------------------------------------------------------
	public unsafe D2D3D9InteropClient(Action<Direct2D1.RenderTarget> renderer, Action<Direct2D1.RenderTarget> resourcesUpdater, Action<nint> backBufferSetter)
	{
		ArgumentNullException.ThrowIfNull(renderer);
		ArgumentNullException.ThrowIfNull(resourcesUpdater);
		m_Renderer = renderer;
		m_ResourcesUpdater = resourcesUpdater;
		m_InteropClient = new D3D9InteropClient(backBufferSetter);
		fixed (D3D11.ID3D11Device** ppDevice = &m_D3D11Device.Put())
		fixed (D3D11.ID3D11DeviceContext** ppContext = &m_D3D11Context.Put())
		PInvoke.D3D11CreateDevice(null, D3D.D3D_DRIVER_TYPE.D3D_DRIVER_TYPE_HARDWARE, HMODULE.Null,
			D3D11.D3D11_CREATE_DEVICE_FLAG.D3D11_CREATE_DEVICE_BGRA_SUPPORT, null, 0,
			PInvoke.D3D11_SDK_VERSION, ppDevice, null, ppContext).ThrowOnFailure();
	}
	public void Dispose()
	{
		m_D2D1RenderTarget.Dispose();
		m_D3D11RenderTarget.Dispose();
		D2D1Factory.Dispose();
		m_D3D11Context.Dispose();
		m_D3D11Device.Dispose();
		m_InteropClient.Dispose();
	}
	public unsafe void CreateAndBindTargets(double width, double height, double dpiScaleX, double dpiScaleY)
	{
		m_InteropClient.SetRenderTarget(null);
		m_D2D1RenderTarget.Release();
		m_D3D11RenderTarget.Release();
		var viewportWidth = Math.Max(width, 100.0);
		var viewportHeight = Math.Max(height, 100.0);
		{
			var renderDesc = new D3D11.D3D11_TEXTURE2D_DESC
			{
				BindFlags = D3D11.D3D11_BIND_FLAG.D3D11_BIND_RENDER_TARGET | D3D11.D3D11_BIND_FLAG.D3D11_BIND_SHADER_RESOURCE,
				Format = Dxgi.Common.DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM,
				Width = (uint)(viewportWidth * dpiScaleX),
				Height = (uint)(viewportHeight * dpiScaleY),
				MipLevels = 1,
				SampleDesc = { Count = 1, Quality = 0 },
				Usage = D3D11.D3D11_USAGE.D3D11_USAGE_DEFAULT,
				MiscFlags = D3D11.D3D11_RESOURCE_MISC_FLAG.D3D11_RESOURCE_MISC_SHARED,
				CPUAccessFlags = 0,
				ArraySize = 1,
			};
			fixed (D3D11.ID3D11Texture2D** pp = &m_D3D11RenderTarget.Put())
				m_D3D11Device.Pointer->CreateTexture2D(renderDesc, null, pp);
		}
		using (var surface = Interop.ComUtils.Cast<Dxgi.IDXGISurface>(m_D3D11RenderTarget.Pointer))
		{
			var rtp = new D2D.D2D1_RENDER_TARGET_PROPERTIES
			{
				type = D2D.D2D1_RENDER_TARGET_TYPE.D2D1_RENDER_TARGET_TYPE_DEFAULT,
				pixelFormat = { format = Dxgi.Common.DXGI_FORMAT.DXGI_FORMAT_UNKNOWN, alphaMode = D2D.Common.D2D1_ALPHA_MODE.D2D1_ALPHA_MODE_PREMULTIPLIED, },
				dpiX = (float)(dpiScaleX * 96.0),
				dpiY = (float)(dpiScaleY * 96.0),
				usage = D2D.D2D1_RENDER_TARGET_USAGE.D2D1_RENDER_TARGET_USAGE_NONE,
				minLevel = D2D.D2D1_FEATURE_LEVEL.D2D1_FEATURE_LEVEL_DEFAULT,
			};
			fixed (D2D.ID2D1RenderTarget** pp = &m_D2D1RenderTarget.Put())
				D2D1Factory.Pointer->CreateDxgiSurfaceRenderTarget(surface.Pointer, rtp, pp);
		}
		m_ResourcesUpdater(m_D2D1RenderTarget);
		m_InteropClient.SetRenderTarget(m_D3D11RenderTarget.Pointer);
		var viewport = new D3D11.D3D11_VIEWPORT
		{
			TopLeftX = 0,
			TopLeftY = 0,
			Width = (float)viewportWidth,
			Height = (float)viewportHeight,
			MinDepth = 0,
			MaxDepth = 1,
		};
		m_D3D11Context.Pointer->RSSetViewports(new(in viewport));
	}
	public unsafe void PrepareAndCallRender()
	{
		m_D2D1RenderTarget.Pointer->BeginDraw();
		m_Renderer(m_D2D1RenderTarget);
		m_D2D1RenderTarget.Pointer->EndDraw(null, null).ThrowOnFailure();
		m_D3D11Context.Pointer->Flush();
	}

	public bool IsD3D9RenderTargetValid => m_InteropClient.IsRenderTargetValid;
	public Direct2D1.Factory D2D1Factory { get; } = new();
}
