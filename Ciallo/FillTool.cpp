#include "pch.hpp"
#include "FillTool.h"

#include "BrushManager.h"
#include "ArrangementManager.h"
#include "RenderingSystem.h"
#include "Canvas.h"
#include "InnerBrush.h"
#include "TempLayers.h"
#include "TimelineManager.h"
#include "StrokeContainer.h"

void FillTool::PadVisRim()
{
	entt::entity drawingE = R.ctx().get<TimelineManager>().GetCurrentDrawing();
	auto& arm = R.get<ArrangementManager>(drawingE);
	auto& vis = arm.Visibility;
	auto& canvas = R.ctx().get<Canvas>();

	glm::vec2 min = canvas.Viewport.Min;
	glm::vec2 max = canvas.Viewport.Max;
	auto points = ArrangementManager::VecToPoints({min, {max.x, min.y}, max, {min.x, max.y}, min});
	vis.insert(points);
}

FillTool::FillTool()
{
	// TODO:should change Radius, Label, delta threshold, SampleInterval
	auto& brushM = R.ctx().get<BrushManager>();
	Painter.BrushE = brushM.Brushes[0];
	Painter.Usage = StrokeUsageFlags::Zone;
	Painter.FillColor = glm::vec4(1.0f, 1.0f, 1.0f, 0.5f);
	Painter.MinRadius = 0.002f;
	Painter.MaxRadius = 0.002f;
	Painter.Color = glm::vec4(18, 18, 129, 255) / 255.0f;
}

std::string FillTool::GetName()
{
	return "Fill";
}

void FillTool::OnClickOrDragStart(ClickOrDragStart event)
{
	Painter.OnDragStart(event);
}

void FillTool::OnDragging(Dragging event)
{
	Painter.OnDragging(event);
}

void FillTool::Activate()
{
	entt::entity drawingE = R.ctx().get<TimelineManager>().GetCurrentDrawing();
	if (drawingE == entt::null) return;
	auto& arm = R.get<ArrangementManager>(drawingE);
	auto& vis = arm.Visibility;
	auto& arr = arm.Arrangement;

	vis.attach(arr);
	PadVisRim();
}

void FillTool::Deactivate()
{
	entt::entity currentE = R.ctx().get<TimelineManager>().GetCurrentDrawing();
	if (currentE == entt::null) return;
	auto& arm = R.get<ArrangementManager>(currentE);

	arm.Visibility.detach();
}

void FillTool::DrawProperties()
{
	ImGui::ColorEdit4("Marker Color##0", glm::value_ptr(Painter.Color), ImGuiColorEditFlags_DisplayRGB);
	ImGui::ColorEdit4("Fill Color##1", glm::value_ptr(Painter.FillColor), ImGuiColorEditFlags_DisplayRGB);
	const float ratio = 1000.0f;
	float minRadiusUI = Painter.MinRadius * ratio;
	if (ImGui::DragFloat("Min Radius(milimeter)##2", &minRadiusUI, 0.01f, 0.1f, 10.0f, "%.2f", ImGuiSliderFlags_ClampOnInput))
	{
		Painter.MinRadius = minRadiusUI / ratio;
	}
	if(ImGui::IsKeyPressed(ImGuiKey_Z)) {
		entt::entity currentE = R.ctx().get<TimelineManager>().GetCurrentDrawing();
		if (currentE == entt::null) return;
		auto& arm = R.get<ArrangementManager>(currentE);
		auto& strokeEs = R.get<StrokeContainer>(currentE).StrokeEs;
		entt::entity strokeE = strokeEs.back();
		auto& usage = R.get<StrokeUsageFlags>(strokeEs.back());
		strokeEs.pop_back();
		if (!!(usage & StrokeUsageFlags::Arrange))
			R.get<ArrangementManager>(currentE).Remove(strokeE);
		if (!!(usage & StrokeUsageFlags::Zone))
			R.get<ArrangementManager>(currentE).RemoveQuery(strokeE);
		R.destroy(strokeE);
	}
}

void FillTool::OnHovering(Hovering event)
{
	auto& canvas = R.ctx().get<Canvas>();
	auto& layers = R.ctx().get<TempLayers>();
	layers.Overlay.BindFramebuffer();
	canvas.Viewport.UploadMVP();
	canvas.Viewport.BindMVPBuffer();
	glEnable(GL_BLEND);
	glBlendFuncSeparate(GL_SRC_ALPHA, GL_ONE_MINUS_SRC_ALPHA, GL_ONE, GL_ONE_MINUS_SRC_ALPHA);
	glEnable(GL_STENCIL_TEST);
	glDisable(GL_DEPTH_TEST);

	entt::entity currentE = R.ctx().get<TimelineManager>().GetCurrentDrawing();
	if (currentE == entt::null) return;
	auto& arm = R.get<ArrangementManager>(currentE);
	// In vis mode
	//if (ImGui::IsKeyDown(ImGuiKey_Space))
	//{
	//	auto polygon = arm.PointQueryVisibility(event.MousePos);

	//	glUseProgram(RenderingSystem::Polygon->Program);
	//	glUniform4fv(1, 1, glm::value_ptr(Painter.FillColor)); // color
	//	ColorFace face{{polygon}};
	//	face.GenUploadBuffers();
	//	face.FillDrawCall();

	//	glDisable(GL_STENCIL_TEST);

	//	Brush& brush = R.ctx().get<InnerBrush>().Get("vanilla");
	//	brush.Use();
	//	glUniform1f(2, Painter.MinRadius);
	//	glUniform4fv(1, 1, glm::value_ptr(Painter.FillColor));
	//	face.LineDrawCall();
	//}
	// In fill preview mode
	if (ImGui::IsKeyDown(ImGuiKey_LeftAlt))
	{
		auto polygonWithHoles = arm.PointQuery(event.MousePos);
		if (!polygonWithHoles.empty())
		{
			glUseProgram(RenderingSystem::Polygon->Program);
			glUniform4fv(1, 1, glm::value_ptr(Painter.FillColor)); // color
			ColorFace face{polygonWithHoles};
			face.GenUploadBuffers();
			face.FillDrawCall();
		}
		glDisable(GL_STENCIL_TEST);
	}
	glBindFramebuffer(GL_FRAMEBUFFER, 0);
}
