#pragma once

#include "Brush.h"
#include "Stroke.h"

class BrushManager
{
public:
	std::vector<std::unique_ptr<Brush>> Brushes{};
	Stroke s;

	void RenderPreview();
	void Draw();
	void OutputPreview();
};

