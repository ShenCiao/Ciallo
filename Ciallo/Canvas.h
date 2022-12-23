#pragma once

#include "Canvas.h"
#include "Viewport.h"
#include "RenderableTexture.h"

class Canvas
{
public:
	RenderableTexture Image{};

	float DrawingRotation = 0.0f;
	float Zoom = 1.0f;
	glm::vec2 Scroll{0.0f, 0.0f};
	Viewport Viewport{};
	float Dpi = 0.0f;

	void DrawUI();
	void GenRenderTarget();
};

