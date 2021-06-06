#pragma once

#define STRINGIFY_INTERNAL(s) #s
#define STRINGIFY(s) STRINGIFY_INTERNAL(s)

namespace Lyriser::Core
{
	void ThrowIfFailed(HRESULT hr)
	{
		if (FAILED(hr))
			System::Runtime::InteropServices::Marshal::ThrowExceptionForHR(hr);
	}

	public value struct ColorF
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
	};
}

namespace Lyriser::Core::Direct2D1
{
	public value struct RectF
	{
	public:
		static RectF FromLTRB(float left, float top, float right, float bottom) { return { left, top, right, bottom }; }
		static RectF FromXYWH(float x, float y, float width, float height) { return { x, y, x + width, y + height }; }

		float Left;
		float Top;
		float Right;
		float Bottom;
	};

	public value struct Point2F
	{
	public:
		Point2F(float x, float y) : X(x), Y(y) {}

		float X;
		float Y;
	};

	public value struct SizeF
	{
	public:
		SizeF(float width, float height) : Width(width), Height(height) {}

		float Width;
		float Height;

	internal:
		SizeF(D2D1_SIZE_F size) : Width(size.width), Height(size.height) { }
	};
}
