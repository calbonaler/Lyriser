using System;
using Windows.Win32.System.Com;

namespace Lyriser.Core.Interop;

static class ComUtils
{
	public static unsafe ref TTo* As<TFrom, TTo>(ref TFrom* from) where TFrom : unmanaged where TTo : unmanaged
		=> ref ((delegate*<ref TFrom*, ref TTo*>)IdPtr)(ref from);

	public static unsafe ref void* AsVoid<TFrom>(ref TFrom* from) where TFrom : unmanaged
		=> ref ((delegate*<ref TFrom*, ref void*>)IdPtr)(ref from);

	public static unsafe ComPtr<T> Cast<T>(void* ptr) where T : unmanaged
	{
		var result = new ComPtr<T>();
		((IUnknown*)ptr)->QueryInterface(typeof(T).GUID, out result.PutVoid()).ThrowOnFailure();
		return result;
	}
	
	static unsafe delegate*<nint, nint> IdPtr => &Id;

	static nint Id(nint x) => x;
}

public abstract unsafe class ComPtrBase
{
	protected ComPtrBase() { _pointer = null; }

	IUnknown* _pointer;
	
	internal void Release()
	{
		if (_pointer != null)
		{
			_pointer->Release();
			_pointer = null;
		}
	}
	internal ref IUnknown* Put()
	{
		Release();
		return ref _pointer;
	}
	internal ref void* PutVoid() => ref ComUtils.AsVoid(ref Put());

	internal IUnknown* Pointer => _pointer;
}

public abstract class DisposableComPtrBase : ComPtrBase, IDisposable
{
	~DisposableComPtrBase() => Dispose(false);
	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}
	protected virtual void Dispose(bool disposing) { Release(); }
}

unsafe class ComPtr<T> : DisposableComPtrBase where T : unmanaged
{
	internal new T* Pointer => (T*)base.Pointer;
	internal new ref T* Put() => ref ComUtils.As<IUnknown, T>(ref base.Put());
}
