using System;
using System.ComponentModel;
using System.Windows;
using Microsoft.Xaml.Behaviors;

namespace Lyriser.Views;

public class WindowClosingBehavior : Behavior<Window>
{
	public static readonly DependencyProperty MethodProperty = DependencyProperty.Register(nameof(Method), typeof(Func<bool>), typeof(WindowClosingBehavior));

	public Func<bool>? Method
	{
		get => (Func<bool>)GetValue(MethodProperty);
		set => SetValue(MethodProperty, value);
	}

	protected override void OnAttached()
	{
		base.OnAttached();
		AssociatedObject.Closing += OnClosing;
	}

	protected override void OnDetaching()
	{
		AssociatedObject.Closing -= OnClosing;
		base.OnDetaching();
	}

	void OnClosing(object? sender, CancelEventArgs e)
	{
		if (Method is not null && !Method())
			e.Cancel = true;
	}
}
