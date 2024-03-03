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
	readonly D3DImage _d3DImage;
	Core.D2D3D9InteropClient? _interopClient;
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
		_d3DImage = new D3DImage();
		if (IsInDesignMode)
			return;
		_d3DImage.IsFrontBufferAvailableChanged += OnIsFrontBufferAvailableChanged;
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
		_interopClient = new();
		RecreateRenderTarget();
		StartRendering();
	}
	void OnUnloaded(object sender, RoutedEventArgs e)
	{
		StopRendering();
		_d3DImage.IsFrontBufferAvailableChanged -= OnIsFrontBufferAvailableChanged;
		Utils.SafeDispose(ref _interopClient);
	}
	void OnRendering(object? sender, EventArgs e)
	{
		Debug.Assert(_interopClient != null, "Rendering is listened to but Loaded is not called");
		if (!_d3DImage.IsFrontBufferAvailable || _interopClient.BackBuffer == 0 || _interopClient.RenderTarget == null)
			return;
		if (DateTime.UtcNow - _lastRenderTargetParamChanged > RenderTargetParmChangesThrottleInterval)
		{
			_lastRenderTargetParamChanged = DateTime.MaxValue;
			RecreateRenderTarget();
		}
		_d3DImage.Lock();
		_d3DImage.SetBackBuffer(D3DResourceType.IDirect3DSurface9, _interopClient.BackBuffer);
		_interopClient.BeginDraw();
		Render(_interopClient.RenderTarget);
		_interopClient.EndDraw();
		_d3DImage.AddDirtyRect(new Int32Rect(0, 0, _d3DImage.PixelWidth, _d3DImage.PixelHeight));
		_d3DImage.Unlock();
	}
	protected override void OnRender(DrawingContext dc) => dc.DrawImage(_d3DImage, new Rect(default, RenderSize));
	protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
	{
		if (_interopClient != null)
			_lastRenderTargetParamChanged = DateTime.UtcNow;
		base.OnRenderSizeChanged(sizeInfo);
	}
	protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
	{
		if (_interopClient != null)
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
		Debug.Assert(_interopClient != null, "Not initialized");
		var dpiScale = VisualTreeHelper.GetDpi(this);
		_interopClient.RecreateRenderTarget(ActualWidth, ActualHeight, dpiScale.DpiScaleX, dpiScale.DpiScaleY);
		ResourceCache.UpdateResources(_interopClient.RenderTarget);
	}
	void StartRendering() => CompositionTarget.Rendering += OnRendering;
	void StopRendering() => CompositionTarget.Rendering -= OnRendering;
}

public class ResourceCache
{
	// - field -----------------------------------------------------------------------
	readonly Dictionary<string, Func<Core.Direct2D1.RenderTarget, object>> _generators = [];
	readonly Dictionary<string, object> _resources = [];
	Core.Direct2D1.RenderTarget? _renderTarget = null;

	// - property --------------------------------------------------------------------
	public object this[string key] => _resources[key];

	// - public methods --------------------------------------------------------------
	public void Add(string key, Func<Core.Direct2D1.RenderTarget, object> generator)
	{
		_generators.Add(key, generator);
		if (_resources.TryGetValue(key, out var resOld))
			(resOld as IDisposable)?.Dispose();
		if (_renderTarget != null)
			_resources[key] = generator(_renderTarget);
	}
	public void UpdateResources(Core.Direct2D1.RenderTarget renderTarget)
	{
		_renderTarget = renderTarget;
		if (_renderTarget == null)
			return;
		foreach (var kvp in _generators)
		{
			if (_resources.TryGetValue(kvp.Key, out var resOld))
				(resOld as IDisposable)?.Dispose();
			_resources[kvp.Key] = kvp.Value(_renderTarget);
		}
	}
}
