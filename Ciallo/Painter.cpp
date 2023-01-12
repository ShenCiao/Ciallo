#include "pch.hpp"
#include "Painter.h"

#include "Stroke.h"
#include "StrokeContainer.h"
#include "ArrangementManager.h"

void Painter::OnDragStart(ClickOrDragStart event)
{
	auto& strokes = R.ctx().get<StrokeContainer>().StrokeEs;
	entt::entity e = R.create();
	auto& s = R.emplace<Stroke>(e);
	R.emplace<StrokeUsageFlags>(e, Usage);

	s.Position = {event.MousePos};
	s.BrushE = Brush;
	s.Thickness = Thickness;
	s.FillColor = FillColor;
	s.UpdateBuffers();
	auto& arm = R.ctx().get<ArrangementManager>();
	if (!!(Usage & StrokeUsageFlags::Arrange))
	{
		arm.AddOrUpdate(e);
	}

	if (!!(Usage & StrokeUsageFlags::Zone))
	{
		arm.AddOrUpdateQuery(e);
	}

	LastSampleDuration = decltype(LastSampleDuration)::zero();
	LastSampleMousePosPixel = event.MousePosPixel;

	strokes.push_back(e);
}

void Painter::OnDragging(Dragging event)
{
	glm::vec2 delta = glm::abs(glm::vec2(event.MousePosPixel - LastSampleMousePosPixel));

	if (event.DragDuration - LastSampleDuration > SampleInterval && delta.x + delta.y >= 6.0f)
	{
		auto& strokes = R.ctx().get<StrokeContainer>().StrokeEs;
		entt::entity e = strokes.back();
		auto& s = R.get<Stroke>(e);
		s.Position.push_back(event.MousePos);
		s.UpdateBuffers();

		auto& arm = R.ctx().get<ArrangementManager>();
		if (!!(Usage & StrokeUsageFlags::Arrange))
		{
			arm.AddOrUpdate(e);
		}

		if (!!(Usage & StrokeUsageFlags::Zone))
		{
			arm.AddOrUpdateQuery(e);
		}

		// TODO: update arrangement or signal changing?
		LastSampleMousePosPixel = event.MousePosPixel;
		LastSampleDuration = event.DragDuration;
	}
}
