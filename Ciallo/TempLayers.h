#pragma once

#include "RenderableTexture.h"

// Used to catch up the siggraph DDL
class TempLayers
{
public:
	RenderableTexture Overlay;
	RenderableTexture Drawing;
	RenderableTexture Fill;

	TempLayers(glm::ivec2 size);
	void RenderDrawing();
	void RenderFill();
	void BlendAll();
	void ClearOverlay();
};

