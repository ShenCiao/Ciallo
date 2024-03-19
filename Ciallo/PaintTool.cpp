#include "pch.hpp"
#include "PaintTool.h"

#include "BrushManager.h"
#include "TimelineManager.h"
#include "StrokeContainer.h"
#include "ArrangementManager.h"
#include "TextureManager.h"
#include "EyedropperInfo.h"
#include "SelectionManager.h"

PaintTool::PaintTool()
{
	auto& brushM = R.ctx().get<BrushManager>();
	Painter.BrushE = brushM.Brushes[2];
	Painter.Usage = StrokeUsageFlags::Arrange | StrokeUsageFlags::Final;
	Painter.SampleInterval =  chrono::duration<float>(0.01f);
	Painter.MinRadius = 0.5e-3f;
	Painter.MaxRadius = 1e-3f;
}

void PaintTool::OnClickOrDragStart(ClickOrDragStart event)
{
	Painter.OnDragStart(event);
}

void PaintTool::OnDragging(Dragging event)
{
	Painter.OnDragging(event);
}

std::string PaintTool::GetName()
{
	return "Paint";
}

void PaintTool::DrawProperties()
{
	ImGui::CheckboxFlags("Show in final image", reinterpret_cast<unsigned*>(&Painter.Usage),
		                     static_cast<unsigned>(StrokeUsageFlags::Final));

	bool clicked = ImGui::CheckboxFlags("Fill color", reinterpret_cast<unsigned*>(&Painter.Usage),
		                     static_cast<unsigned>(StrokeUsageFlags::Fill));
	if (clicked) {
		if (!!(Painter.Usage & StrokeUsageFlags::Fill)) {
			Painter.Usage = Painter.Usage & ~StrokeUsageFlags::Arrange;
		}
		else {
			Painter.Usage = Painter.Usage | StrokeUsageFlags::Arrange;
		}
	}
	Painter.DrawProperties();
	if (ImGui::IsKeyPressed(ImGuiKey_Z)) {
		entt::entity e = R.ctx().get<SelectionManager>().StrokeContainerE;
		if (e == entt::null) return;
		auto& strokes = R.get<StrokeContainer>(e).StrokeEs;

		entt::entity strokeE = strokes.back();

		auto& strokeUsage = R.get<StrokeUsageFlags>(strokeE);
		auto currentDrawingE = R.ctx().get<TimelineManager>().GetCurrentDrawing();
		if (currentDrawingE == entt::null) return;
		if (!!(strokeUsage & StrokeUsageFlags::Arrange))
			R.get<ArrangementManager>(currentDrawingE).Remove(strokeE);
		if (!!(strokeUsage & StrokeUsageFlags::Zone))
			R.get<ArrangementManager>(currentDrawingE).RemoveQuery(strokeE);

		strokes.pop_back();
		R.destroy(strokeE);
	}
	int w, h;
	int miplevel = 0;
	glGetTextureLevelParameteriv(TextureManager::Textures[5], miplevel, GL_TEXTURE_WIDTH, &w);
	glGetTextureLevelParameteriv(TextureManager::Textures[5], miplevel, GL_TEXTURE_HEIGHT, &h);
	const float width = ImGui::GetWindowContentRegionWidth();

	/*ImGui::Image(reinterpret_cast<ImTextureID>(TextureManager::Textures[5]), { width, width * float(h)/float(w)});*/

	auto& info = R.ctx().get<EyedropperInfo>();
	if(ImGui::IsKeyDown(ImGuiKey_I)) {
		info.IsPicking = true;
		Painter.FillColor = glm::vec4(info.Color, 1.0);
		ImGui::ColorTooltip("Color", glm::value_ptr(info.Color), 0);
	}

	if (ImGui::IsKeyReleased(ImGuiKey_I)) {
		info.IsPicking = false;
	}
}
