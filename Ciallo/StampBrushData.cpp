#include "pch.hpp"
#include "StampBrushData.h"

void StampBrushData::SetUniforms()
{
	glBindTexture(GL_TEXTURE_2D, StampTexture);
	glUniform1f(4, StampIntervalRatio);
}
