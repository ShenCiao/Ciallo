#pragma once

#include "CanvasEvent.h"

enum class StrokeUsageFlags
{
	Default = 0,		// on final image, no fill
	Label = 1 << 0,		// not on final image
	Fill = 1 << 1,		// fill polygon color
	Loop = 1 << 2,		// draw line loop
	Arrange = 1 << 3,	// insert into arrangement
	Zone = 1 << 4,		// calculate zone
	_entt_enum_as_bitmask
};


class Painter
{
	chrono::duration<float> LastSampleDuration{0.0f};
	chrono::duration<float> SampleInterval{ 0.01f }; // 10ms
	glm::vec2 LastSampleMousePosPixel{};
public:
	entt::entity Brush = entt::null;
	glm::vec4 Color = {0.0f, 0.0f, 0.0f, 1.0f};
	float Thickness = 0.001f;
	StrokeUsageFlags Usage = StrokeUsageFlags::Default;

	void OnDragStart(ClickOrDragStart event);
	void OnDragging(Dragging event);
};

