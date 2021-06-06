#pragma once

namespace Lyriser::Core::Interop
{
	template <typename T> struct remove_handle { using type = T; };
	template <typename T> struct remove_handle<T^> { using type = T; };

	template <typename T>
	public ref class ComPtrBase abstract
	{
	public:
		using pointer = T*;

	protected:
		ComPtrBase() : p(nullptr) { }

	internal:
		void Release()
		{
			if (p)
			{
				p->Release();
				p = nullptr;
			}
		}

		interior_ptr<pointer> GetAddress() { return &p; }
		interior_ptr<pointer> ReleaseAndGetAddress()
		{
			Release();
			return &p;
		}

		pointer p;

	private protected:
		void AddRef() { if (p) p->AddRef(); }
		void Assign(pointer p)
		{
			Release();
			this->p = p;
			AddRef();
		}
	};

	template <typename T>
	public ref class DisposableComPtrBase abstract : ComPtrBase<T>
	{
	public:
		~DisposableComPtrBase() { this->!DisposableComPtrBase(); }
		!DisposableComPtrBase() { Release(); }
	};

	template <typename T>
	ref class LightComPtr : public DisposableComPtrBase<T>
	{
	public:
		LightComPtr% operator =(T* p)
		{
			Assign(p);
			return *this;
		}
	};

#define PIN_LIGHT_COM_PTR_FOR_SET(ptr) \
	pin_ptr<Lyriser::Core::Interop::remove_handle<decltype(ptr)>::type::pointer> pinned_ ## ptr = ptr->ReleaseAndGetAddress(); \
	Lyriser::Core::Interop::remove_handle<decltype(ptr)>::type::pointer* p ## ptr = pinned_ ## ptr
#define PIN_COM_PTR_FOR_SET(ptr) \
	pin_ptr<decltype(ptr)::pointer> pinned_ ## ptr = ptr.ReleaseAndGetAddress(); \
	decltype(ptr)::pointer* p ## ptr = pinned_ ## ptr

	template <typename T>
	ref class ComPtr : public DisposableComPtrBase<T>
	{
	public:
		ComPtr() { }
		explicit ComPtr(T* p)
		{
			DisposableComPtrBase<T>::p = p;
			AddRef();
		}

		ComPtr% operator =(T* p)
		{
			Assign(p);
			return *this;
		}

		T* operator ->() { return p; }

		explicit operator bool() { return p != nullptr; }
	};
}
