#pragma once

#define CS_IN [System::Runtime::InteropServices::InAttribute, System::Runtime::CompilerServices::IsReadOnlyAttribute]
#define CS_READONLY [System::Runtime::CompilerServices::IsReadOnlyAttribute]

#define DEFINE_STRUCT_EQUALITY_OP(r, data, i, elem) BOOST_PP_IF(i, &&, ) elem == other.elem
#define DEFINE_STRUCT_EQUALITY(T, ...) \
    CS_READONLY virtual bool Equals(T other) { \
		return BOOST_PP_SEQ_FOR_EACH_I(DEFINE_STRUCT_EQUALITY_OP,, BOOST_PP_TUPLE_TO_SEQ((__VA_ARGS__))); \
	} \
	CS_READONLY bool Equals(Object^ obj) override { return obj != nullptr && obj->GetType() == T::typeid && Equals(safe_cast<T>(obj)); } \
    CS_READONLY int GetHashCode() override { return System::HashCode::Combine(__VA_ARGS__); } \
	static bool operator ==(T left, T right) { return left.Equals(right); } \
	static bool operator !=(T left, T right) { return !(left == right); }

namespace Lyriser::Core
{
	void ThrowIfFailed(HRESULT hr)
	{
		if (FAILED(hr))
			System::Runtime::InteropServices::Marshal::ThrowExceptionForHR(hr);
	}

	public value struct ColorF : public System::IEquatable<ColorF>
	{
	public:
		ColorF(float red, float green, float blue, float alpha) : Red(red), Green(green), Blue(blue), Alpha(alpha) {}
		ColorF(float red, float green, float blue) : Red(red), Green(green), Blue(blue), Alpha(1) {}
		ColorF(int rgb, float alpha) : Red(((rgb >> 16) & 0xff) / 255.0f), Green(((rgb >> 8) & 0xff) / 255.0f), Blue((rgb & 0xff) / 255.0f), Alpha(alpha) {}
		ColorF(int rgb) : Red(((rgb >> 16) & 0xff) / 255.0f), Green(((rgb >> 8) & 0xff) / 255.0f), Blue((rgb & 0xff) / 255.0f), Alpha(1) {}

		float Red;
		float Green;
		float Blue;
		float Alpha;

		DEFINE_STRUCT_EQUALITY(ColorF, Red, Green, Blue, Alpha);
	};
}

namespace Lyriser::Core::Direct2D1
{
	public value struct RectF : public System::IEquatable<RectF>
	{
	public:
		static RectF FromLTRB(System::Numerics::Vector2 topLeft, System::Numerics::Vector2 bottomRight)
		{
			RectF rect{};
			rect.TopLeft = topLeft;
			rect.BottomRight = bottomRight;
			return rect;
		}
		static RectF FromLTRB(float left, float top, float right, float bottom) { return FromLTRB(System::Numerics::Vector2(left, top), System::Numerics::Vector2(right, bottom)); }
		static RectF FromXYWH(System::Numerics::Vector2 location, System::Numerics::Vector2 size) { return FromLTRB(location, location + size); }
		static RectF FromXYWH(float x, float y, float width, float height) { return FromXYWH(System::Numerics::Vector2(x, y), System::Numerics::Vector2(width, height)); }

		System::Numerics::Vector2 TopLeft;
		System::Numerics::Vector2 BottomRight;
		property System::Numerics::Vector2 Size
		{
			CS_READONLY System::Numerics::Vector2 get() { return BottomRight - TopLeft; }
			void set(System::Numerics::Vector2 value) { BottomRight = TopLeft + value; }
		}

		DEFINE_STRUCT_EQUALITY(RectF, TopLeft, BottomRight);
	};
}
