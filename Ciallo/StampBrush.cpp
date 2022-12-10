#include "pch.hpp"
#include "StampBrush.h"

void StampBrush::SetUniform()
{
	Brush::SetUniform();
	glBindTexture(GL_TEXTURE_2D, Stamp);
	glUniform1f(4, StampIntervalRatio);
}
