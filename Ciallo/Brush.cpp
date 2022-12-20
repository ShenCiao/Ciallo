#include "pch.hpp"
#include "Brush.h"

void Brush::SetUniform()
{
	if (AirBrush)
	{
		AirBrush->SetUniform();
		return;
	}
	if (Stamp)
	{
		Stamp->SetUniform();
		return;
	}
}

void Brush::Use()
{
	glUseProgram(Program);
}
