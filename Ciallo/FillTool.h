#pragma once

#include "Tool.h"

class FillTool : public Tool
{
	chrono::duration<float, std::milli> LastSampleDuration{ 0.0f };
	chrono::duration<float, std::milli> SampleInterval{ 25.f };

	glm::vec2 LastSampleMousePos{};

	glm::vec4 PolygonColor = {0.0f, 0.0f, 0.0f, 1.0f};
public:
	explicit FillTool(CanvasPanel* canvas)
		: Tool(canvas)
	{
	}

	void ClickOrDragStart() override;
	void Dragging() override;
	void DrawProperties() override;
};
