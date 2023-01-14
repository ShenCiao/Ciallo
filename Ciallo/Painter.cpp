#include "pch.hpp"
#include "Painter.h"

#include "Stroke.h"
#include "StrokeContainer.h"
#include "ArrangementManager.h"
#include "BrushManager.h"
#include "Brush.h"

void Painter::OnDragStart(ClickOrDragStart event)
{
	auto& strokes = R.ctx().get<StrokeContainer>().StrokeEs;
	entt::entity e = R.create();
	auto& s = R.emplace<Stroke>(e);
	R.emplace<StrokeUsageFlags>(e, Usage);

	s.Position = {event.MousePos};
	s.BrushE = BrushE;
	s.Thickness = Thickness;
	s.Color = Color;
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
		
		LastSampleMousePosPixel = event.MousePosPixel;
		LastSampleDuration = event.DragDuration;
	}
}

void Painter::DrawProperties()
{
	auto& brush = R.get<Brush>(BrushE);
	const float width = ImGui::GetWindowContentRegionWidth();
	ImGui::Image(reinterpret_cast<ImTextureID>(brush.PreviewTexture.ColorTexture),
	             {width, width / 2.0f / glm::golden_ratio<float>()});
	if (ImGui::Button("Edit Brush"))
		R.ctx().get<BrushManager>().OpenBrushEditor(&BrushE);

	ImGui::ColorPicker4("Line Color", glm::value_ptr(Color), ImGuiColorEditFlags_DisplayRGB);
	if(!!(Usage&StrokeUsageFlags::Fill) || !!(Usage&StrokeUsageFlags::Zone))
		ImGui::ColorPicker4("Fill Color", glm::value_ptr(FillColor), ImGuiColorEditFlags_DisplayRGB);
	ImGui::DragFloat("Thickness", &Thickness, 0.0001f, 0.0001f, 0.010f, "%.4f", ImGuiSliderFlags_ClampOnInput);
}
