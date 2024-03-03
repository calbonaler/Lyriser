using System;
using System.Diagnostics.CodeAnalysis;
using Windows.Win32.Foundation;
using PInvoke = Windows.Win32.PInvoke;
using D2D = Windows.Win32.Graphics.Direct2D;
using D3D = Windows.Win32.Graphics.Direct3D;
using D3D9 = Windows.Win32.Graphics.Direct3D9;
using D3D11 = Windows.Win32.Graphics.Direct3D11;
using Dxgi = Windows.Win32.Graphics.Dxgi;

namespace Lyriser.Core;

public sealed unsafe class D2D3D9InteropClient : IDisposable
{
	// - field -----------------------------------------------------------------------
	readonly Interop.ComPtr<D3D9.IDirect3DDevice9Ex> m_D3D9Device = new();
	readonly Interop.ComPtr<D3D11.ID3D11Device> m_D3D11Device = new();
	readonly Interop.ComPtr<D3D11.ID3D11DeviceContext> m_D3D11Context = new();
	Interop.ComPtr<D3D9.IDirect3DSurface9>? m_D3D9RenderTarget;

	// - public methods --------------------------------------------------------------
	public D2D3D9InteropClient()
	{
		using (var context = new Interop.ComPtr<D3D9.IDirect3D9Ex>())
		{
			fixed (D3D9.IDirect3D9Ex** pp = &context.Put())
				PInvoke.Direct3DCreate9Ex(PInvoke.D3D_SDK_VERSION, pp).ThrowOnFailure();
			fixed (D3D9.IDirect3DDevice9Ex** pp = &m_D3D9Device.Put())
			{
				var presentParams = new D3D9.D3DPRESENT_PARAMETERS
				{
					BackBufferWidth = 1,
					BackBufferHeight = 1,
					SwapEffect = D3D9.D3DSWAPEFFECT.D3DSWAPEFFECT_DISCARD,
					Windowed = true,
				};
				context.Pointer->CreateDeviceEx(0, D3D9.D3DDEVTYPE.D3DDEVTYPE_HAL, HWND.Null,
					PInvoke.D3DCREATE_HARDWARE_VERTEXPROCESSING | PInvoke.D3DCREATE_MULTITHREADED | PInvoke.D3DCREATE_FPU_PRESERVE,
					&presentParams, null, pp);
			}
		}
		fixed (D3D11.ID3D11Device** ppDevice = &m_D3D11Device.Put())
		fixed (D3D11.ID3D11DeviceContext** ppContext = &m_D3D11Context.Put())
		{
			PInvoke.D3D11CreateDevice(null, D3D.D3D_DRIVER_TYPE.D3D_DRIVER_TYPE_HARDWARE, null,
				D3D11.D3D11_CREATE_DEVICE_FLAG.D3D11_CREATE_DEVICE_BGRA_SUPPORT, null, 0,
				PInvoke.D3D11_SDK_VERSION, ppDevice, null, ppContext).ThrowOnFailure();
		}
	}
	public void Dispose()
	{
		RenderTarget?.ComPtr.Dispose();
		RenderTarget = null;
		m_D3D9RenderTarget?.Dispose();
		m_D3D9RenderTarget = null;
		m_D3D11Context.Dispose();
		m_D3D11Device.Dispose();
		m_D3D9Device.Dispose();
		D2D1Factory.Dispose();
	}
	[MemberNotNull(nameof(RenderTarget))]
	public void RecreateRenderTarget(double width, double height, double dpiScaleX, double dpiScaleY)
	{
		ThrowIfDisposed();
		RenderTarget?.ComPtr.Dispose();
		RenderTarget = null;
		m_D3D9RenderTarget?.Dispose();
		m_D3D9RenderTarget = null;
		void* sharedHandle = null;
		var d3d9RenderTarget = new Interop.ComPtr<D3D9.IDirect3DSurface9>();
		try
		{
			fixed (D3D9.IDirect3DSurface9** pp = &d3d9RenderTarget.Put())
				m_D3D9Device.Pointer->CreateRenderTarget((uint)(width * dpiScaleX), (uint)(height * dpiScaleY),
					D3D9.D3DFORMAT.D3DFMT_A8R8G8B8, D3D9.D3DMULTISAMPLE_TYPE.D3DMULTISAMPLE_NONE, 0, false, pp, &sharedHandle);
			m_D3D9RenderTarget = d3d9RenderTarget;
			d3d9RenderTarget = null;
		}
		finally { d3d9RenderTarget?.Dispose(); }
		using var surface = new Interop.ComPtr<Dxgi.IDXGISurface>();
		fixed (nint* pp = &surface.PutIntPtr())
		{
			var guid = typeof(Dxgi.IDXGISurface).GUID;
			m_D3D11Device.Pointer->OpenSharedResource(sharedHandle, &guid, pp);
		}
		var renderTarget = new Interop.ComPtr<D2D.ID2D1RenderTarget>();
		try
		{
			fixed (D2D.ID2D1RenderTarget** pp = &renderTarget.Put())
			{
				var rtp = new D2D.D2D1_RENDER_TARGET_PROPERTIES
				{
					pixelFormat = { alphaMode = D2D.Common.D2D1_ALPHA_MODE.D2D1_ALPHA_MODE_PREMULTIPLIED, },
					dpiX = (float)(dpiScaleX * 96.0),
					dpiY = (float)(dpiScaleY * 96.0),
				};
				D2D1Factory.Pointer->CreateDxgiSurfaceRenderTarget(surface.Pointer, &rtp, pp);
			}
			RenderTarget = new Direct2D1.RenderTarget(renderTarget);
			renderTarget = null;
		}
		finally { renderTarget?.Dispose(); }
	}
	public void BeginDraw()
	{
		ThrowIfDisposed();
		ThrowIfRenderTargetNotCreated();
		RenderTarget.ComPtr.Pointer->BeginDraw();
	}
	public void EndDraw()
	{
		ThrowIfDisposed();
		ThrowIfRenderTargetNotCreated();
		RenderTarget.ComPtr.Pointer->EndDraw(null, null);
		m_D3D11Context.Pointer->Flush();
	}
	void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(m_D3D11Context.IsNull || m_D3D11Device.IsNull || m_D3D9Device.IsNull || D2D1Factory.IsNull, this);
	[MemberNotNull(nameof(RenderTarget))]
	void ThrowIfRenderTargetNotCreated()
	{
		if (RenderTarget == null)
			throw new InvalidOperationException($"{nameof(RenderTarget)} has not been created via {nameof(RecreateRenderTarget)}");
	}

	public Direct2D1.Factory D2D1Factory { get; } = new();
	public Direct2D1.RenderTarget? RenderTarget { get; private set; }
	public nint BackBuffer => m_D3D9RenderTarget == null ? 0 : (nint)m_D3D9RenderTarget.Pointer;
}
