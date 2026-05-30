using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Lyriser;

static class PropertyChangedUtils
{
	public static void Raise(this PropertyChangedEventHandler? handler, INotifyPropertyChanged @this, [CallerMemberName] string propertyName = "") => handler?.Invoke(@this, new(propertyName));

	public static void SetWithRelated<T>(ref T storage, T value, PropertyChangedEventHandler? handler, INotifyPropertyChanged @this, IEnumerable<string> relatedPropertyNames, [CallerMemberName] string propertyName = "")
	{
		if (!EqualityComparer<T>.Default.Equals(storage, value))
		{
			storage = value;
			handler.Raise(@this, propertyName);
			foreach (var relatedPropertyName in relatedPropertyNames)
				handler.Raise(@this, relatedPropertyName);
		}
	}

	public static void Set<T>(ref T storage, T value, PropertyChangedEventHandler? handler, INotifyPropertyChanged @this, [CallerMemberName] string propertyName = "") => SetWithRelated(ref storage, value, handler, @this, [], propertyName);
}
