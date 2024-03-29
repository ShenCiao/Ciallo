#pragma once

#include "RenderableTexture.h"
#include "Stroke.h"

// Used to catch up the siggraph DDL
class TempLayers
{
public:
	RenderableTexture Overlay;
	RenderableTexture Drawing;
	RenderableTexture Fill;

	bool FinalOnly = false;
	bool HideFill = false;

	TempLayers(glm::ivec2 size);
	void RenderOverlay();
	void RenderDrawing();
	void RenderFill();
	void BlendAll();
	void ClearOverlay();
private:
	// Used by overlay, shitty design
	Stroke Circle;
	std::vector<Stroke> Lines;

	void GenCircleStroke();
};

