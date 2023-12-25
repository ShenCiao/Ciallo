#include "pch.hpp"
#include "Painter.h"

#include "Stroke.h"
#include "StrokeContainer.h"
#include "ArrangementManager.h"
#include "BrushManager.h"
#include "Brush.h"
#include "implot.h"
#include "TimelineManager.h"

void Painter::OnDragStart(ClickOrDragStart event)
{
	entt::entity drawingE = R.ctx().get<TimelineManager>().GetCurrentDrawing();
	if (drawingE == entt::null) return;
	auto& strokes = R.get<StrokeContainer>(drawingE).StrokeEs;
	entt::entity e = R.create();
	auto& s = R.emplace<Stroke>(e);
	R.emplace<StrokeUsageFlags>(e, Usage);

	s.Position = {event.MousePos};
	s.BrushE = BrushE;
	s.Radius = MinRadius;
	s.RadiusOffset.push_back(PressureToRadiusOffset(event.Pressure));
	s.Color = Color;
	s.FillColor = FillColor;
	s.UpdateBuffers();
	entt::entity currentE = R.ctx().get<TimelineManager>().GetCurrentDrawing();
	if (currentE == entt::null) return;
	auto& arm = R.get<ArrangementManager>(currentE);
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
		entt::entity drawingE = R.ctx().get<TimelineManager>().GetCurrentDrawing();
		if (drawingE == entt::null) return;
		auto& strokes = R.get<StrokeContainer>(drawingE).StrokeEs;
		entt::entity e = strokes.back();
		auto& s = R.get<Stroke>(e);
		s.Position.push_back(event.MousePos);
		s.RadiusOffset.push_back(PressureToRadiusOffset(event.Pressure));
		s.UpdateBuffers();

		entt::entity currentE = R.ctx().get<TimelineManager>().GetCurrentDrawing();
		if (currentE == entt::null) return;
		auto& arm = R.get<ArrangementManager>(currentE);
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

	ImGui::ColorEdit4("Line Color##0", glm::value_ptr(Color), ImGuiColorEditFlags_DisplayRGB);
	if (!!(Usage & StrokeUsageFlags::Fill) || !!(Usage & StrokeUsageFlags::Zone))
		ImGui::ColorEdit4("Fill Color##1", glm::value_ptr(FillColor), ImGuiColorEditFlags_DisplayRGB);
	const float ratio = 1000.0f;
	float minRadiusUI = MinRadius * ratio;
	float maxRadiusUI = MaxRadius * ratio;
	if (ImGui::DragFloat("Min Radius(milimeter)##2", &minRadiusUI, 0.01f, 0.1f, maxRadiusUI, "%.2f", ImGuiSliderFlags_ClampOnInput))
	{
		MinRadius = minRadiusUI/ratio;
	}
	if (ImGui::DragFloat("Max Radius(milimeter)##3", &maxRadiusUI, 0.01f, minRadiusUI, 30.0f, "%.2f", ImGuiSliderFlags_ClampOnInput))
	{
		MaxRadius = maxRadiusUI/ratio;
	}

	// This is despairingly messy. Fix it someday.
	ImGui::TextUnformatted("Pen Pressure");
	ImPlotAxisFlags axFlags = ImPlotAxisFlags_NoTickLabels | ImPlotAxisFlags_NoTickMarks | ImPlotAxisFlags_Lock;
	if (ImPlot::BeginPlot("Pen Pressure##Bezier", ImVec2(-1, 0), ImPlotFlags_CanvasOnly))
	{
		// This stupid thing only accept double.
		ImPlot::SetupAxes("Pressure", "Radius", axFlags, axFlags);
		ImPlot::SetupAxesLimits(0, 1, 0, 1);
		auto& cps = RaidusMappingCurve.ControlPoints;
		ImPlotPoint P[] = {
			{cps[0].x, cps[0].y}, {cps[1].x, cps[1].y}, {cps[2].x, cps[2].y}, {cps[3].x, cps[3].y}
		};
		auto pFlags = ImPlotDragToolFlags_Delayed;
		ImPlot::DragPoint(1, &P[1].x, &P[1].y, ImVec4(1, 0.5f, 1, 1), 4, pFlags);
		ImPlot::DragPoint(2, &P[2].x, &P[2].y, ImVec4(0, 0.5f, 1, 1), 4, pFlags);

		const int n = 32;
		ImPlotPoint B[n];
		for (int i = 0; i < n; ++i)
		{
			double t = static_cast<double>(i) / n;
			double u = 1 - t;
			double w1 = u * u * u;
			double w2 = 3 * u * u * t;
			double w3 = 3 * u * t * t;
			double w4 = t * t * t;
			B[i] = ImPlotPoint(w1 * P[0].x + w2 * P[1].x + w3 * P[2].x + w4 * P[3].x,
			                   w1 * P[0].y + w2 * P[1].y + w3 * P[2].y + w4 * P[3].y);
		}

		
		for(int i = 0; i < 4; ++i)
		{
			glm::vec2 p = {P[i].x, P[i].y};
			cps[i] = glm::clamp(p, {0.0f, 0.0f}, {1.0f, 1.0f});
		}
		RaidusMappingCurve.EvalLUT();

		ImPlot::SetNextLineStyle(ImVec4(1, 0.5f, 1, 1));
		ImPlot::PlotLine("##h1", &P[0].x, &P[0].y, 2, 0, 0, sizeof(ImPlotPoint));
		ImPlot::SetNextLineStyle(ImVec4(0, 0.5f, 1, 1));
		ImPlot::PlotLine("##h2", &P[2].x, &P[2].y, 2, 0, 0, sizeof(ImPlotPoint));
		ImPlot::SetNextLineStyle(ImVec4(0, 0.9f, 0, 1), 2);
		ImPlot::PlotLine("##bez", &B[0].x, &B[0].y, n, 0, 0, sizeof(ImPlotPoint));

		ImPlot::EndPlot();
	}
}

float Painter::PressureToRadiusOffset(float pressure)
{
	float t = RaidusMappingCurve.FindT(pressure);
	return (MaxRadius - MinRadius) * RaidusMappingCurve(t).y;
}
