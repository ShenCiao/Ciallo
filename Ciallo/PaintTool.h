#pragma once

#include "Tool.h"

class PaintTool : public Tool
{
public:
	explicit PaintTool(CanvasPanel* panel)
		: Tool(panel)
	{
	}

	void ClickOrDragStart() override;
};
