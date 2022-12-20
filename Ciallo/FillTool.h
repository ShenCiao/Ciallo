#pragma once

#include "Stroke.h"
#include "Tool.h"

class FillTool : public Tool
{
	chrono::duration<float, std::milli> LastSampleDuration{ 0.0f };
	chrono::duration<float, std::milli> SampleInterval{ 25.f };

	std::unique_ptr<Stroke> Rim;
	entt::entity ActiveBrush;

	glm::vec2 LastSampleMousePos{};

	glm::vec4 PolygonColor = {0.0f, 0.0f, 0.0f, 1.0f};
public:
	explicit FillTool(CanvasPanel* canvas)
		: Tool(canvas)
	{
	}

	void PadRim();
	void RemoveRim();

	void ClickOrDragStart() override;
	void Dragging() override;
	void Activate() override;
	void Deactivate() override;
	void DrawProperties() override;
	void Hovering() override;
};
