using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace Lyriser.Views;

public abstract class D2dControl : FrameworkElement
{
	// - field -----------------------------------------------------------------------
	const double UnconstrainedContentSize = 1;
	static readonly TimeSpan RenderTargetParmChangesThrottleInterval = TimeSpan.FromMilliseconds(250);
	readonly D3DImage m_D3DImage;
	Core.D2D3D9InteropClient? m_InteropClient;
	DateTime _lastRenderTargetParamChanged = DateTime.MaxValue;

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
		m_D3DImage = new D3DImage();
		if (IsInDesignMode)
			return;
		m_D3DImage.IsFrontBufferAvailableChanged += OnIsFrontBufferAvailableChanged;
		Loaded += OnLoaded;
		Unloaded += OnUnloaded;
	}
	public abstract void Render(Core.Direct2D1.RenderTarget target);
	protected override Size MeasureOverride(Size constraint)
	{
		if (double.IsPositiveInfinity(constraint.Width))
			constraint.Width = UnconstrainedContentSize;
		if (double.IsPositiveInfinity(constraint.Height))
			constraint.Height = UnconstrainedContentSize;
		return constraint;
	}

	// - event handler ---------------------------------------------------------------
	void OnLoaded(object sender, RoutedEventArgs e)
	{
		m_InteropClient = new();
		RecreateRenderTarget();
		StartRendering();
	}
	void OnUnloaded(object sender, RoutedEventArgs e)
	{
		StopRendering();
		m_D3DImage.IsFrontBufferAvailableChanged -= OnIsFrontBufferAvailableChanged;
		Utils.SafeDispose(ref m_InteropClient);
	}
	void OnRendering(object? sender, EventArgs e)
	{
		Debug.Assert(m_InteropClient != null, "Rendering is listened to but Loaded is not called");
		if (!m_D3DImage.IsFrontBufferAvailable || m_InteropClient.BackBuffer == 0 || m_InteropClient.RenderTarget == null)
			return;
		if (DateTime.UtcNow - _lastRenderTargetParamChanged > RenderTargetParmChangesThrottleInterval)
		{
			_lastRenderTargetParamChanged = DateTime.MaxValue;
			RecreateRenderTarget();
		}
		m_D3DImage.Lock();
		m_D3DImage.SetBackBuffer(D3DResourceType.IDirect3DSurface9, m_InteropClient.BackBuffer);
		m_InteropClient.BeginDraw();
		Render(m_InteropClient.RenderTarget);
		m_InteropClient.EndDraw();
		m_D3DImage.AddDirtyRect(new Int32Rect(0, 0, m_D3DImage.PixelWidth, m_D3DImage.PixelHeight));
		m_D3DImage.Unlock();
	}
	protected override void OnRender(DrawingContext dc) => dc.DrawImage(m_D3DImage, new Rect(default, RenderSize));
	protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
	{
		if (m_InteropClient != null)
			_lastRenderTargetParamChanged = DateTime.UtcNow;
		base.OnRenderSizeChanged(sizeInfo);
	}
	protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
	{
		if (m_InteropClient != null)
			_lastRenderTargetParamChanged = DateTime.UtcNow;
		base.OnDpiChanged(oldDpi, newDpi);
	}
	void OnIsFrontBufferAvailableChanged(object sender, DependencyPropertyChangedEventArgs e)
	{
		if ((bool)e.NewValue)
			StartRendering();
		else
			StopRendering();
	}

	// - private methods -------------------------------------------------------------
	void RecreateRenderTarget()
	{
		Debug.Assert(m_InteropClient != null, "Not initialized");
		var dpiScale = VisualTreeHelper.GetDpi(this);
		m_InteropClient.RecreateRenderTarget(ActualWidth, ActualHeight, dpiScale.DpiScaleX, dpiScale.DpiScaleY);
		ResourceCache.UpdateResources(m_InteropClient.RenderTarget);
	}
	void StartRendering() => CompositionTarget.Rendering += OnRendering;
	void StopRendering() => CompositionTarget.Rendering -= OnRendering;
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
