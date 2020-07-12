using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;

namespace Lyriser
{
	static class Utils
	{
		public static void AssignWithDispose<T>(ref T storage, T value) where T : IDisposable
		{
			storage?.Dispose();
			storage = value;
		}

		public static void SafeDispose<T>(ref T storage) where T : IDisposable => AssignWithDispose(ref storage, default);

		public static void Raise(this PropertyChangedEventHandler handler, INotifyPropertyChanged @this, [CallerMemberName]string propertyName = "") => handler?.Invoke(@this, new PropertyChangedEventArgs(propertyName));

		public static void SetPropertyWithRelated<T>(ref T storage, T value, PropertyChangedEventHandler handler, INotifyPropertyChanged @this, IEnumerable<string> relatedPropertyNames, [CallerMemberName]string propertyName = "")
		{
			if (!EqualityComparer<T>.Default.Equals(storage, value))
			{
				storage = value;
				handler.Raise(@this, propertyName);
				foreach (var relatedPropertyName in relatedPropertyNames)
					handler.Raise(@this, relatedPropertyName);
			}
		}

		public static void SetProperty<T>(ref T storage, T value, PropertyChangedEventHandler handler, INotifyPropertyChanged @this, [CallerMemberName]string propertyName = "") => SetPropertyWithRelated(ref storage, value, handler, @this, Enumerable.Empty<string>(), propertyName);

		public static IObservable<PropertyChangedEventArgs> AsPropertyChanged(this INotifyPropertyChanged source) =>
			Observable.FromEventPattern<PropertyChangedEventArgs>(x => source.PropertyChanged += x.Invoke, x => source.PropertyChanged -= x.Invoke).Select(x => x.EventArgs);

		public static IObservable<PropertyChangedEventArgs> AsPropertyChanged(this INotifyPropertyChanged source, string propertyName) => AsPropertyChanged(source).Where(x => x.PropertyName == propertyName);

		public static double Clamp(double value, double min, double max)
		{
			if (value < min)
				return min;
			if (value > max)
				return max;
			return value;
		}
	}
}
