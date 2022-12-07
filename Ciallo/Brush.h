#pragma once

#include "RenderableTexture.h"

class Brush
{
public:
	// Opengl objects are owned by other managers;
	GLuint Program;
	GLenum TextureTarget = GL_TEXTURE_2D;
	GLuint Stamp = 0; // Stamp or gradient in airbrush
	glm::vec4 Color = {0.0f, 0.0f, 0.0f, 1.0f};
	std::optional<float> UniformThickness;

	RenderableTexture PreviewTexture;
	

	void Use()
	{
		glUseProgram(Program);
		glBindTexture(TextureTarget, Stamp);
		glUniform4fv(1, 1, glm::value_ptr(Color)); // color
		if (UniformThickness)
			glUniform1f(2, *UniformThickness);
	}
};
