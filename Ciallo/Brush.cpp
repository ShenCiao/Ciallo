#include "pch.hpp"
#include "Brush.h"

Brush::Brush(const Brush& other): Program(other.Program),
                                  Name(other.Name),
                                  PreviewTexture(other.PreviewTexture)
{
	AirBrush = std::make_unique<AirBrushData>(*other.AirBrush);
	Stamp = std::make_unique<StampBrushData>(*other.Stamp);
}

Brush::Brush(Brush&& other) noexcept: Program(other.Program),
                                      Name(std::move(other.Name)),
                                      PreviewTexture(std::move(other.PreviewTexture)),
                                      AirBrush(std::move(other.AirBrush)),
                                      Stamp(std::move(other.Stamp))
{
}

Brush& Brush::operator=(Brush other)
{
	using std::swap;
	swap(*this, other);
	return *this;
}

void Brush::SetUniforms() const
{
	if (AirBrush)
	{
		AirBrush->SetUniforms();
		return;
	}
	if (Stamp)
	{
		Stamp->SetUniforms();
		return;
	}
}

void Brush::Use() const
{
	glUseProgram(Program);
}

void Brush::DrawProperties()
{
	if (Stamp)
	{
		Stamp->DrawProperties();
		return;
	}
	if (AirBrush)
	{
		AirBrush->DrawProperties();
		AirBrush->UpdateGradient();
		return;
	}
}

void swap(Brush& lhs, Brush& rhs) noexcept
{
	using std::swap;
	swap(lhs.Program, rhs.Program);
	swap(lhs.Name, rhs.Name);
	swap(lhs.PreviewTexture, rhs.PreviewTexture);
	swap(lhs.AirBrush, rhs.AirBrush);
	swap(lhs.Stamp, rhs.Stamp);
}
