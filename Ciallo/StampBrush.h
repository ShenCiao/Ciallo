#pragma once

#include "Brush.h"

class StampBrush : public Brush
{
public:
	GLuint Stamp = 0;
	float StampIntervalRatio = 0;

	void SetUniform() override;
};

