#pragma once

#include "RenderableTexture.h"

// Move this into `class Canvas` perhaps?
class CanvasSelectionTextureManager
{
public:
	RenderableTexture SelectionTexture;

	CanvasSelectionTextureManager();

	void GenSelectionTexture(glm::ivec2 size);
	void RenderSelectionTexture();

	static glm::vec4 IndexToColor(uint32_t index);
	static uint32_t ColorToIndex(glm::vec4 color);
};