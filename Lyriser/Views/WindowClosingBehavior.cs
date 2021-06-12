using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Xaml.Behaviors;
using Ast = System.Linq.Expressions.Expression;

namespace Lyriser.Views
{
	public class WindowClosingBehavior : Behavior<Window>
	{
		public static readonly DependencyProperty MethodTargetProperty = DependencyProperty.Register(nameof(MethodTarget), typeof(object), typeof(WindowClosingBehavior), new PropertyMetadata(null));
		public static readonly DependencyProperty MethodNameProperty = DependencyProperty.Register(nameof(MethodName), typeof(string), typeof(WindowClosingBehavior), new PropertyMetadata(null));

		readonly ReturnMethodBinder<bool> binder = new();

		public object MethodTarget
		{
			get => GetValue(MethodTargetProperty);
			set => SetValue(MethodTargetProperty, value);
		}

		public string MethodName
		{
			get => (string)GetValue(MethodNameProperty);
			set => SetValue(MethodNameProperty, value);
		}

		protected override void OnAttached()
		{
			var associatedObject = AssociatedObject;
			if (associatedObject == null) throw new InvalidOperationException();

			base.OnAttached();
			var canContinue = false;
			associatedObject.Closing += async (sender, e) =>
			{
				if (canContinue) return;
				if (MethodTarget == null || MethodName == null) return;
				e.Cancel = true;
				canContinue = await binder.Invoke(MethodTarget, MethodName);
				DispatcherOperation op;
				if (canContinue)
					op = Dispatcher.BeginInvoke(new Action(associatedObject.Close));
			};
		}
	}

	public class ReturnMethodBinder<T>
	{
		Type? _cachedTargetType;
		string? _cachedName;
		Func<object, Task<T>>? _cachedDelegate;

		public Task<T> Invoke(object target, string name)
		{
			if (target == null)
				throw new ArgumentNullException(nameof(target));
			if (name == null)
				throw new ArgumentNullException(nameof(name));
			var targetType = target.GetType();
			if (_cachedTargetType == targetType && _cachedName == name)
			{
				Debug.Assert(_cachedDelegate != null);
				return _cachedDelegate(target);
			}

			_cachedTargetType = targetType;
			_cachedName = name;
			var method = targetType.GetMethod(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy, null, Type.EmptyTypes, null);
			if (method == null)
				throw new ArgumentException($"指定されたオブジェクトの型 ({targetType}) に指定されたメソッド ({name}) は存在しません。");
			if (method.ReturnType == typeof(T))
			{
				var targetParam = Ast.Parameter(typeof(object), nameof(target));
				_cachedDelegate = Ast.Lambda<Func<object, Task<T>>>(
					Ast.Call(typeof(Task), "FromResult", new[] { typeof(T) }, Ast.Call(Ast.Convert(targetParam, targetType), method)),
					targetParam
				).Compile();
				return _cachedDelegate(target);
			}
			if (method.ReturnType == typeof(Task<T>))
			{
				var targetParam = Ast.Parameter(typeof(object), nameof(target));
				_cachedDelegate = Ast.Lambda<Func<object, Task<T>>>(
					Ast.Call(Ast.Convert(targetParam, targetType), method),
					targetParam
				).Compile();
				return _cachedDelegate(target);
			}
			throw new ArgumentException($"指定されたオブジェクトの型 ({targetType}) に指定された戻り値の型 ({typeof(T)}) のメソッド ({name}) は存在しません。");
		}
	}
}
