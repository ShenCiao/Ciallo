#pragma once

#include "Brush.h"
#include "CubicBezier.h"

class AirBrush : public Brush
{
public:
	constexpr static int SampleCount = 32;
	GLuint Gradient = 0;
	CubicBezier Curve;

	AirBrush();
	AirBrush(const AirBrush& other) = delete;
	AirBrush(AirBrush&& other) noexcept = delete;
	AirBrush& operator=(const AirBrush& other) = delete;
	AirBrush& operator=(AirBrush&& other) noexcept = delete;
	~AirBrush() override;
	void UpdateGradient();
	void SetUniform() override;
};

