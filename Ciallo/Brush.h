#pragma once

#include "RenderableTexture.h"

class Brush
{
public:
	virtual ~Brush() = default;
	// Opengl objects are owned by other managers;
	GLuint Program;
	glm::vec4 Color = {0.0f, 0.0f, 0.0f, 1.0f};

	std::string Name;
	RenderableTexture PreviewTexture;

	virtual void SetUniform();
	void Use();
};
