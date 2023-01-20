#include "pch.hpp"
#include "FillTool.h"

#include "BrushManager.h"
#include "ArrangementManager.h"
#include "RenderingSystem.h"
#include "Canvas.h"
#include "InnerBrush.h"
#include "TempLayers.h"

void FillTool::PadVisRim()
{
	auto& vis = R.ctx().get<ArrangementManager>().Visibility;
	auto& canvas = R.ctx().get<Canvas>();

	glm::vec2 min = canvas.Viewport.Min;
	glm::vec2 max = canvas.Viewport.Max;
	auto points = ArrangementManager::VecToPoints({min, {max.x, min.y}, max, {min.x, max.y}, min});
	vis.insert(points);
}

FillTool::FillTool()
{
	// TODO:should change Thickness, Label, delta threshold, SampleInterval
	auto& brushM = R.ctx().get<BrushManager>();
	Painter.BrushE = brushM.Brushes[0];
	Painter.Usage = StrokeUsageFlags::Zone;
	Painter.Thickness = 0.0005f;
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
	auto& arm = R.ctx().get<ArrangementManager>();
	auto& vis = arm.Visibility;
	auto& arr = arm.Arrangement;

	vis.attach(arr);
	PadVisRim();
}

void FillTool::Deactivate()
{
	auto& vis = R.ctx().get<ArrangementManager>().Visibility;

	vis.detach();
}

void FillTool::DrawProperties()
{
	Painter.DrawProperties();
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
	// In vis mode
	if (ImGui::IsKeyDown(ImGuiKey_LeftAlt))
	{
		auto polygon = R.ctx().get<ArrangementManager>().PointQueryVisibility(event.MousePos);

		glUseProgram(RenderingSystem::Polygon->Program);
		glUniform4fv(1, 1, glm::value_ptr(Painter.FillColor)); // color
		ColorFace face{{polygon}};
		face.GenUploadBuffers();
		face.FillDrawCall();

		glDisable(GL_STENCIL_TEST);

		Brush& brush = R.ctx().get<InnerBrush>().Get("vanilla");
		brush.Use();
		glUniform1f(2, Painter.Thickness);
		glUniform4fv(1, 1, glm::value_ptr(Painter.FillColor));
		face.LineDrawCall();
	}
	// In fill preview mode
	else
	{
		auto polygonWithHoles = R.ctx().get<ArrangementManager>().PointQuery(event.MousePos);
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
