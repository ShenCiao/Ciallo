#pragma once

#include "CanvasEvent.h"

enum class StrokeUsageFlags
{
	Zero = 0,		// on final image, no fill
	Final = 1u << 1,	// on final image
	Line = 1 << 2,		// draw line
	Fill = 1 << 3,		// draw fill enclosed by stroke
	Arrange = 1 << 4,	// insert into arrangement
	Zone = 1 << 5,		// query zone
	_entt_enum_as_bitmask
};


class Painter
{
	chrono::duration<float> LastSampleDuration{0.0f};
	
	glm::vec2 LastSampleMousePosPixel{};
public:
	chrono::duration<float> SampleInterval{ 0.01f }; // 10ms
	entt::entity BrushE = entt::null;
	glm::vec4 Color = {0.0f, 0.0f, 0.0f, 1.0f};
	float Radius = 0.001f;
	glm::vec4 FillColor = {0.0f, 0.0f, 0.0f, 1.0f};
	StrokeUsageFlags Usage = StrokeUsageFlags::Zero;

	void OnDragStart(ClickOrDragStart event);
	void OnDragging(Dragging event);
	void DrawProperties();
};