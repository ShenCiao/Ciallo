#pragma once

#include "Brush.h"
#include "RenderableTexture.h"
#include "Stroke.h"

class BrushManager
{
public:
	std::vector<std::unique_ptr<Brush>> Brushes{};
	Stroke s;

	RenderableTexture Preview;

	void RenderPreview();
	void Draw();
	
};

