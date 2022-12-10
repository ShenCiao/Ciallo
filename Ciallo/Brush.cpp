#include "pch.hpp"
#include "Brush.h"

void Brush::SetUniform()
{
	glUniform4fv(1, 1, glm::value_ptr(Color));
}

void Brush::Use()
{
	glUseProgram(Program);
}
