#include "pch.hpp"
#include "AirBrushData.h"

AirBrushData::AirBrushData()
{
}

AirBrushData::AirBrushData(const AirBrushData& other): Curve(other.Curve)
{
	UpdateGradient();
}

AirBrushData::AirBrushData(AirBrushData&& other) noexcept: Gradient(other.Gradient),
                                               Curve(std::move(other.Curve))
{
	other.Gradient = 0;
}

AirBrushData& AirBrushData::operator=(AirBrushData other)
{
	using std::swap;
	swap(*this, other);
	return *this;
}

AirBrushData::~AirBrushData()
{
	glDeleteTextures(1, &Gradient);
}

void AirBrushData::UpdateGradient()
{
	glDeleteTextures(1, &Gradient);
	glCreateTextures(GL_TEXTURE_1D, 1, &Gradient);
	glTextureParameteri(Gradient, GL_TEXTURE_MIN_FILTER, GL_LINEAR);
	glTextureParameteri(Gradient, GL_TEXTURE_MAG_FILTER, GL_LINEAR);
	glTextureParameteri(Gradient, GL_TEXTURE_WRAP_S, GL_MIRRORED_REPEAT);
	glTextureParameteri(Gradient, GL_TEXTURE_WRAP_T, GL_MIRRORED_REPEAT);
	glTextureStorage1D(Gradient, 1, GL_RGBA32F, SampleCount);

	std::vector<float> values(SampleCount);
	for (int i = 0; i < SampleCount; ++i)
	{
		float x = static_cast<float>(i) / (SampleCount - 1);
		float t = Curve.FindT(x);
		if (t == std::numeric_limits<float>::min())
			throw std::runtime_error("Fail to find y value");
		values[i] = Curve(t).y;
	}
	glTextureSubImage1D(Gradient, 0, 0, SampleCount, GL_RED, GL_FLOAT, values.data());
}

void AirBrushData::SetUniforms()
{
	glBindTexture(GL_TEXTURE_1D, Gradient);
}

void swap(AirBrushData& lhs, AirBrushData& rhs) noexcept
{
	using std::swap;
	swap(lhs.Gradient, rhs.Gradient);
	swap(lhs.Curve, rhs.Curve);
}
