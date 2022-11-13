#pragma once

#include "Tool.h"

class PaintTool : public Tool
{
	chrono::duration<float, std::milli> LastSample{0.0f};
	chrono::duration<float, std::milli> SampleInterval{ 5.f };
public:
	explicit PaintTool(CanvasPanel* canvas)
		: Tool(canvas)
	{
	}

	void ClickOrDragStart() override;
	void Dragging() override;
};
