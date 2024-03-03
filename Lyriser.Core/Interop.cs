using System;
using System.Runtime.InteropServices;
using Windows.Win32.Foundation;

namespace Lyriser.Core.Interop;

static class ComUtils
{
	public static unsafe ref TTo* As<TFrom, TTo>(ref TFrom* from) where TFrom : unmanaged where TTo : unmanaged
		=> ref ((delegate*<ref TFrom*, ref TTo*>)IdPtr)(ref from);

	public static unsafe ref TTo* IntPtrAs<TTo>(ref nint from) where TTo : unmanaged
		=> ref ((delegate*<ref nint, ref TTo*>)IdPtr)(ref from);

	public static unsafe ComPtr<T> Cast<T>(void* obj) where T : unmanaged
	{
		var result = new ComPtr<T>();
		var guid = typeof(T).GUID;
		new HRESULT(Marshal.QueryInterface((nint)obj, ref guid, out result.PutIntPtr())).ThrowOnFailure();
		return result;
	}
	
	static unsafe delegate*<nint, nint> IdPtr => &Id;

	static nint Id(nint x) => x;
}

public class ComPtr : IDisposable
{
	nint _pointer = 0;

	~ComPtr() => Dispose(false);
	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}
	protected virtual void Dispose(bool disposing) => Release();
	internal ref nint PutIntPtr()
	{
		Release();
		return ref _pointer;
	}
	void Release()
	{
		if (_pointer != 0)
		{
			Marshal.Release(_pointer);
			_pointer = 0;
		}
	}

	internal bool IsNull => _pointer == 0;
	internal void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(IsNull, this);

	internal nint Pointer => _pointer;
}

unsafe class ComPtr<T> : ComPtr where T : unmanaged
{
	internal new T* Pointer => (T*)base.Pointer;
	internal ref T* Put() => ref ComUtils.IntPtrAs<T>(ref PutIntPtr());
}
