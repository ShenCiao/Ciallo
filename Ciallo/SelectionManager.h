#pragma once

#include "Application.h"
#include "Application.h"
#include "RenderableTexture.h"

class SelectionManager
{
public:
	RenderableTexture SelectionTexture;

	SelectionManager();

	void GenSelectionTexture(glm::ivec2 size);
	void RenderSelectionTexture();

	glm::vec4 IndexToColor(uint32_t index) const;
	uint32_t ColorToIndex(glm::vec4 color) const;
};