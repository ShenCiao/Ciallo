#include "pch.hpp"
#include "StampBrushData.h"

void StampBrushData::SetUniform()
{
	glBindTexture(GL_TEXTURE_2D, StampTexture);
	glUniform1f(4, StampIntervalRatio);
}
