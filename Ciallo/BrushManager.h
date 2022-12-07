#pragma once

#include "Brush.h"
#include "RenderableTexture.h"

class BrushManager
{
public:
	std::vector<std::unique_ptr<Brush>> Brushes{};

	RenderableTexture Preview;

	void RenderPreview();
	void Draw();
};

