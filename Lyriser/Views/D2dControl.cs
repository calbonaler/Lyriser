using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Interop;

namespace Lyriser.Views;

public abstract class D2dControl : System.Windows.Controls.Image
{
	const int RenderWait = 2; // default: 2ms

	// - field -----------------------------------------------------------------------
	D3DImage? m_D3DImage;
	Core.D2D3D9InteropClient? m_InteropClient;

	// - property --------------------------------------------------------------------
	public static bool IsInDesignMode
	{
		get
		{
			var prop = DesignerProperties.IsInDesignModeProperty;
			var isDesignMode = (bool)DependencyPropertyDescriptor.FromProperty(prop, typeof(FrameworkElement)).Metadata.DefaultValue;
			return isDesignMode;
		}
	}
	protected ResourceCache ResourceCache { get; } = new ResourceCache();

	// - public methods --------------------------------------------------------------
	protected D2dControl()
	{
		Loaded += OnLoaded;
		Unloaded += OnUnloaded;
		Stretch = System.Windows.Media.Stretch.Fill;
	}
	public abstract void Render(Core.Direct2D1.RenderTarget target);

	// - event handler ---------------------------------------------------------------
	void OnLoaded(object sender, RoutedEventArgs e)
	{
		if (IsInDesignMode)
			return;
		m_D3DImage = new D3DImage();
		m_D3DImage.IsFrontBufferAvailableChanged += OnIsFrontBufferAvailableChanged;
		m_InteropClient = new Core.D2D3D9InteropClient(Render, ResourceCache.UpdateResources, OnSetBackBuffer);
		var dpiScaleFactor = DpiScaleFactor;
		m_InteropClient.CreateAndBindTargets(ActualWidth, ActualHeight, dpiScaleFactor.X, dpiScaleFactor.Y);
		Source = m_D3DImage;
		StartRendering();
	}
	void OnUnloaded(object sender, RoutedEventArgs e)
	{
		if (IsInDesignMode)
			return;
		Debug.Assert(m_D3DImage != null, "Unloaded is called but Loaded is not");
		StopRendering();
		m_D3DImage.IsFrontBufferAvailableChanged -= OnIsFrontBufferAvailableChanged;
		Source = null;
		Utils.SafeDispose(ref m_InteropClient);
	}
	void OnRendering(object? sender, EventArgs e)
	{
		Debug.Assert(m_D3DImage != null && m_InteropClient != null, "Rendering is listened to but Loaded is not called");
		m_InteropClient.PrepareAndCallRender();
		if (m_InteropClient.IsD3D9RenderTargetValid)
		{
			m_D3DImage.Lock();
			Thread.Sleep(RenderWait);
			m_D3DImage.AddDirtyRect(new Int32Rect(0, 0, m_D3DImage.PixelWidth, m_D3DImage.PixelHeight));
			m_D3DImage.Unlock();
		}
	}
	protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
	{
		Debug.Assert(m_D3DImage != null && m_InteropClient != null, "RenderSizeChanged is called but Loaded is not");
		var dpiScaleFactor = DpiScaleFactor;
		m_InteropClient.CreateAndBindTargets(ActualWidth, ActualHeight, dpiScaleFactor.X, dpiScaleFactor.Y);
		base.OnRenderSizeChanged(sizeInfo);
	}
	void OnIsFrontBufferAvailableChanged(object sender, DependencyPropertyChangedEventArgs e)
	{
		if ((bool)e.NewValue)
			StartRendering();
		else
			StopRendering();
	}
	void OnSetBackBuffer(IntPtr newBackBuffer)
	{
		Debug.Assert(m_D3DImage != null && m_InteropClient != null, "OnSetBackBuffer is called but Loaded is not");
		m_D3DImage.Lock();
		m_D3DImage.SetBackBuffer(D3DResourceType.IDirect3DSurface9, newBackBuffer);
		m_D3DImage.Unlock();
	}

	// - private methods -------------------------------------------------------------
	void StartRendering() => System.Windows.Media.CompositionTarget.Rendering += OnRendering;
	void StopRendering() => System.Windows.Media.CompositionTarget.Rendering -= OnRendering;
	Vector DpiScaleFactor
	{
		get
		{
			var source = PresentationSource.FromVisual(this);
			return source != null && source.CompositionTarget != null
				? new Vector(source.CompositionTarget.TransformToDevice.M11, source.CompositionTarget.TransformToDevice.M22)
				: new Vector(1, 1);
		}
	}
}

public class ResourceCache
{
	// - field -----------------------------------------------------------------------
	readonly Dictionary<string, Func<Core.Direct2D1.RenderTarget, object>> m_Generators = new();
	readonly Dictionary<string, object> m_Resources = new();
	Core.Direct2D1.RenderTarget? m_RenderTarget = null;

	// - property --------------------------------------------------------------------
	public object this[string key] => m_Resources[key];

	// - public methods --------------------------------------------------------------
	public void Add(string key, Func<Core.Direct2D1.RenderTarget, object> generator)
	{
		m_Generators.Add(key, generator);
		if (m_Resources.TryGetValue(key, out var resOld))
			(resOld as IDisposable)?.Dispose();
		if (m_RenderTarget != null)
			m_Resources[key] = generator(m_RenderTarget);
	}
	public void UpdateResources(Core.Direct2D1.RenderTarget renderTarget)
	{
		m_RenderTarget = renderTarget;
		if (m_RenderTarget == null)
			return;
		foreach (var kvp in m_Generators)
		{
			if (m_Resources.TryGetValue(kvp.Key, out var resOld))
				(resOld as IDisposable)?.Dispose();
			m_Resources[kvp.Key] = kvp.Value(m_RenderTarget);
		}
	}
}
