#pragma once

#include "RenderableTexture.h"

class Brush
{
public:
	// Opengl objects are owned by other managers;
	GLuint Program;
	GLenum TextureTarget = GL_TEXTURE_2D;
	GLuint Stamp = 0; // Stamp or gradient in airbrush
	std::optional<float> StampIntervalRatio;
	glm::vec4 Color = {0.0f, 0.0f, 0.0f, 1.0f};

	std::string Name;
	RenderableTexture PreviewTexture;
	

	void Use()
	{
		glUseProgram(Program);
	}
};
