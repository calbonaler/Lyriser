#pragma once

#include "Common.h"
#include "Interop.h"
#include "Direct2D1.h"

namespace Lyriser::Core
{
	ref class D3D9InteropClient
	{
	public:
		D3D9InteropClient(System::Action<System::IntPtr>^ backBufferSetter)
		{
			if (backBufferSetter == nullptr)
				throw gcnew System::ArgumentNullException(BOOST_PP_STRINGIZE(backBufferSetter));
			{
				msclr::lock lock(s_LockObject);
				if (s_ActiveClients++ == 0)
					StartD3D();
			}
			m_BackBufferSetter = backBufferSetter;
		}
		~D3D9InteropClient()
		{
			SetRenderTarget(nullptr);
			msclr::lock lock(s_LockObject);
			if (--s_ActiveClients == 0)
				EndD3D();
		}
		void SetRenderTarget(ID3D11Texture2D* d3D11RenderTarget)
		{
			if (m_D3D9RenderTarget)
			{
				m_BackBufferSetter(System::IntPtr::Zero);
				m_D3D9RenderTarget.Release();
			}
			if (!d3D11RenderTarget)
				return;
			auto format = TranslateFormat(d3D11RenderTarget);
			auto handle = GetSharedHandle(d3D11RenderTarget);
			if (!IsShareable(d3D11RenderTarget))
				throw gcnew System::ArgumentException("Texture must be created with ResouceOptionFlags.Shared", BOOST_PP_STRINGIZE(d3D11RenderTarget));
			if (format == D3DFMT_UNKNOWN)
				throw gcnew System::ArgumentException("Texture format is not compatible with OpenSharedResouce", BOOST_PP_STRINGIZE(d3D11RenderTarget));
			if (handle == nullptr)
				throw gcnew System::ArgumentException("Invalid handle", BOOST_PP_STRINGIZE(d3D11RenderTarget));
			{
				PIN_COM_PTR_FOR_SET(m_D3D9RenderTarget);
				D3D11_TEXTURE2D_DESC desc;
				d3D11RenderTarget->GetDesc(&desc);
				ThrowIfFailed(s_D3D9Device->p->CreateTexture(desc.Width, desc.Height, 1, D3DUSAGE_RENDERTARGET, format, D3DPOOL_DEFAULT, pm_D3D9RenderTarget, &handle));
			}
			{
				Interop::ComPtr<IDirect3DSurface9> surface;
				PIN_COM_PTR_FOR_SET(surface);
				ThrowIfFailed(m_D3D9RenderTarget->GetSurfaceLevel(0, psurface));
				m_BackBufferSetter(System::IntPtr(surface.p));
			}
		}
		property bool IsRenderTargetValid { bool get() { return safe_cast<bool>(m_D3D9RenderTarget); } }

	private:
		void StartD3D()
		{
			auto presentParams = GetPresentParameters();
			{
				PIN_LIGHT_COM_PTR_FOR_SET(s_D3D9Context);
				ThrowIfFailed(Direct3DCreate9Ex(D3D_SDK_VERSION, ps_D3D9Context));
			}
			{
				PIN_LIGHT_COM_PTR_FOR_SET(s_D3D9Device);
				ThrowIfFailed(s_D3D9Context->p->CreateDeviceEx(0, D3DDEVTYPE_HAL, nullptr, D3DCREATE_HARDWARE_VERTEXPROCESSING | D3DCREATE_MULTITHREADED | D3DCREATE_FPU_PRESERVE, &presentParams, nullptr, ps_D3D9Device));
			}
		}
		void EndD3D()
		{
			m_D3D9RenderTarget.Release();
			s_D3D9Device->Release();
			s_D3D9Context->Release();
		}
		static D3DPRESENT_PARAMETERS GetPresentParameters()
		{
			D3DPRESENT_PARAMETERS pp{};
			pp.Windowed = true;
			pp.SwapEffect = D3DSWAPEFFECT_DISCARD;
			pp.hDeviceWindow = GetDesktopWindow();
			pp.PresentationInterval = D3DPRESENT_INTERVAL_DEFAULT;
			return pp;
		}
		static HANDLE GetSharedHandle(ID3D11Texture2D* texture)
		{
			Interop::ComPtr<IDXGIResource> resource;
			PIN_COM_PTR_FOR_SET(resource);
			ThrowIfFailed(texture->QueryInterface(presource));
			HANDLE sharedHandle;
			ThrowIfFailed(resource->GetSharedHandle(&sharedHandle));
			return sharedHandle;
		}
		static D3DFORMAT TranslateFormat(ID3D11Texture2D* texture)
		{
			D3D11_TEXTURE2D_DESC desc;
			texture->GetDesc(&desc);
			switch (desc.Format)
			{
			case DXGI_FORMAT_R10G10B10A2_UNORM:  return D3DFMT_A2B10G10R10;
			case DXGI_FORMAT_R16G16B16A16_FLOAT: return D3DFMT_A16B16G16R16F;
			case DXGI_FORMAT_B8G8R8A8_UNORM:     return D3DFMT_A8R8G8B8;
			default:                             return D3DFMT_UNKNOWN;
			}
		}
		static bool IsShareable(ID3D11Texture2D* texture)
		{
			D3D11_TEXTURE2D_DESC desc;
			texture->GetDesc(&desc);
			return (desc.MiscFlags & D3D11_RESOURCE_MISC_SHARED) != 0;
		}

		static initonly System::Object^ s_LockObject = gcnew System::Object();
		static initonly Interop::LightComPtr<IDirect3D9Ex>^ s_D3D9Context = gcnew Interop::LightComPtr<IDirect3D9Ex>();
		static initonly Interop::LightComPtr<IDirect3DDevice9Ex>^ s_D3D9Device = gcnew Interop::LightComPtr<IDirect3DDevice9Ex>();
		static int s_ActiveClients;

		Interop::ComPtr<IDirect3DTexture9> m_D3D9RenderTarget;
		System::Action<System::IntPtr>^ m_BackBufferSetter;
	};

	public ref class D2D3D9InteropClient
	{
		// - field -----------------------------------------------------------------------
		Interop::ComPtr<ID3D11Device> m_D3D11Device;
		Interop::ComPtr<ID3D11DeviceContext> m_D3D11Context;
		D3D9InteropClient m_InteropClient;
		Direct2D1::Factory m_D2D1Factory;
		Interop::ComPtr<ID3D11Texture2D> m_D3D11RenderTarget;
		Direct2D1::RenderTargetImpl m_D2D1RenderTarget;

		System::Action<Direct2D1::RenderTarget^>^ m_Renderer;
		System::Action<Direct2D1::RenderTarget^>^ m_ResourcesUpdater;

	public:
		// - public methods --------------------------------------------------------------
		D2D3D9InteropClient(System::Action<Direct2D1::RenderTarget^>^ renderer, System::Action<Direct2D1::RenderTarget^>^ resourcesUpdater, System::Action<System::IntPtr>^ backBufferSetter) : m_InteropClient(backBufferSetter)
		{
			if (renderer == nullptr)
				throw gcnew System::ArgumentNullException(BOOST_PP_STRINGIZE(renderer));
			if (resourcesUpdater == nullptr)
				throw gcnew System::ArgumentNullException(BOOST_PP_STRINGIZE(resourcesUpdater));
			m_Renderer = renderer;
			m_ResourcesUpdater = resourcesUpdater;
			{
				PIN_COM_PTR_FOR_SET(m_D3D11Device);
				PIN_COM_PTR_FOR_SET(m_D3D11Context);
				ThrowIfFailed(D3D11CreateDevice(nullptr, D3D_DRIVER_TYPE_HARDWARE, nullptr, D3D11_CREATE_DEVICE_BGRA_SUPPORT, nullptr, 0, D3D11_SDK_VERSION, pm_D3D11Device, nullptr, pm_D3D11Context));
			}
		}
		void CreateAndBindTargets(double width, double height, double dpiScaleX, double dpiScaleY)
		{
			m_InteropClient.SetRenderTarget(nullptr);
			m_D2D1RenderTarget.Release();
			m_D3D11RenderTarget.Release();
			auto viewportWidth = System::Math::Max(width, 100.0);
			auto viewportHeight = System::Math::Max(height, 100.0);
			{
				D3D11_TEXTURE2D_DESC renderDesc{};
				renderDesc.BindFlags = D3D11_BIND_RENDER_TARGET | D3D11_BIND_SHADER_RESOURCE;
				renderDesc.Format = DXGI_FORMAT_B8G8R8A8_UNORM;
				renderDesc.Width = safe_cast<uint32_t>(viewportWidth * dpiScaleX);
				renderDesc.Height = safe_cast<uint32_t>(viewportHeight * dpiScaleY);
				renderDesc.MipLevels = 1;
				renderDesc.SampleDesc.Count = 1;
				renderDesc.SampleDesc.Quality = 0;
				renderDesc.Usage = D3D11_USAGE_DEFAULT;
				renderDesc.MiscFlags = D3D11_RESOURCE_MISC_SHARED;
				renderDesc.CPUAccessFlags = 0;
				renderDesc.ArraySize = 1;
				PIN_COM_PTR_FOR_SET(m_D3D11RenderTarget);
				ThrowIfFailed(m_D3D11Device->CreateTexture2D(&renderDesc, nullptr, pm_D3D11RenderTarget));
			}
			Interop::ComPtr<IDXGISurface> surface;
			{
				PIN_COM_PTR_FOR_SET(surface);
				ThrowIfFailed(m_D3D11RenderTarget->QueryInterface(psurface));
			}
			{
				D2D1_RENDER_TARGET_PROPERTIES rtp{};
				rtp.type = D2D1_RENDER_TARGET_TYPE_DEFAULT;
				rtp.pixelFormat.format = DXGI_FORMAT_UNKNOWN;
				rtp.pixelFormat.alphaMode = D2D1_ALPHA_MODE_PREMULTIPLIED;
				rtp.dpiX = safe_cast<float>(dpiScaleX * 96.0);
				rtp.dpiY = safe_cast<float>(dpiScaleY * 96.0);
				rtp.usage = D2D1_RENDER_TARGET_USAGE_NONE;
				rtp.minLevel = D2D1_FEATURE_LEVEL_DEFAULT;
				PIN_COM_PTR_FOR_SET(m_D2D1RenderTarget);
				ThrowIfFailed(m_D2D1Factory.p->CreateDxgiSurfaceRenderTarget(surface.p, rtp, pm_D2D1RenderTarget));
			}
			m_ResourcesUpdater(%m_D2D1RenderTarget);
			m_InteropClient.SetRenderTarget(m_D3D11RenderTarget.p);
			D3D11_VIEWPORT viewport{};
			viewport.TopLeftX = 0;
			viewport.TopLeftY = 0;
			viewport.Width = safe_cast<float>(viewportWidth);
			viewport.Height = safe_cast<float>(viewportHeight);
			viewport.MinDepth = 0;
			viewport.MaxDepth = 1;
			m_D3D11Context->RSSetViewports(1, &viewport);
		}
		void PrepareAndCallRender()
		{
			if (!m_D3D11Context) return;
			m_D2D1RenderTarget.p->BeginDraw();
			m_Renderer(%m_D2D1RenderTarget);
			ThrowIfFailed(m_D2D1RenderTarget.p->EndDraw());
			m_D3D11Context->Flush();
		}

		property bool IsD3D9RenderTargetValid { bool get() { return m_InteropClient.IsRenderTargetValid; } }
		property Direct2D1::Factory^ D2D1Factory { Direct2D1::Factory^ get() { return %m_D2D1Factory; } }
	};
}
