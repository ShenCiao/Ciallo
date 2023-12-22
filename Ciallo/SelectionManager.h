#pragma once

#include "RenderableTexture.h"

class SelectionManager
{
public:
	RenderableTexture SelectionTexture;

	SelectionManager();

	void GenSelectionTexture();
	void RenderSelectionTexture();

	glm::vec4 IndexToColor(uint32_t index) const;
	uint32_t ColorToIndex(glm::vec4 color) const;
};

