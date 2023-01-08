#include "pch.hpp"
#include "Painter.h"

#include "Stroke.h"
#include "StrokeContainer.h"

void Painter::OnDragStart(ClickOrDragStart event)
{
	auto& strokes = R.ctx().get<StrokeContainer>().StrokeEs;
	entt::entity strokeE = R.create();
	auto& s = R.emplace<Stroke>(strokeE);
	R.emplace<StrokeUsageFlags>(strokeE, Usage);

	s.Position = {event.MousePos};
	s.Brush = Brush;
	s.Thickness = Thickness;
	s.UpdateBuffers();
	// TODO: update arrangement or signal changing?
	LastSampleDuration = decltype(LastSampleDuration)::zero();
	LastSampleMousePosPixel = event.MousePosPixel;

	strokes.push_back(strokeE);
}

void Painter::OnDragging(Dragging event)
{
	glm::vec2 delta = glm::abs(glm::vec2(event.MousePosPixel - LastSampleMousePosPixel));
	
	if (event.DragDuration - LastSampleDuration > SampleInterval && delta.x+delta.y >= 6.0f)
	{
		auto& strokes = R.ctx().get<StrokeContainer>().StrokeEs;
		auto& s = R.get<Stroke>(strokes.back());
		s.Position.push_back(event.MousePos);
		s.UpdateBuffers();
		// TODO: update arrangement or signal changing?
		LastSampleMousePosPixel = event.MousePosPixel;
		LastSampleDuration = event.DragDuration;
	}
}
