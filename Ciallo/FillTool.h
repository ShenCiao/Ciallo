#pragma once

#include "Tool.h"

class FillTool : public Tool
{
	chrono::duration<float, std::milli> LastSampleDuration{ 0.0f };
	chrono::duration<float, std::milli> SampleInterval{ 50.f };

	glm::vec2 LastSampleMousePos{};
public:
	explicit FillTool(CanvasPanel* canvas)
		: Tool(canvas)
	{
	}

	void ClickOrDragStart() override;
	void Dragging() override;
};
