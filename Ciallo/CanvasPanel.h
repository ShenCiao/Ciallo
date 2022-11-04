#pragma once

#include "Drawing.h"

class CanvasPanel
{
public:
	Drawing* ActiveDrawing = nullptr;
	float DrawingRotation = 0.0f;
	float Zoom = 1.0f;
	glm::vec2 Scroll{0.0f, 0.0f};

	GLuint image_texture = 0;
	int image_width = 0;
	int image_height = 0;

	void Draw(glm::vec2*);
	void LoadTestImage();
};
