#pragma once

#include "PaintTool.h"

class CanvasPanel;

class Toolbox
{
	std::unique_ptr<PaintTool> PaintTool;
public:
	Toolbox(CanvasPanel* panel);

	Tool* ActiveTool = nullptr;

	void Draw();
};
