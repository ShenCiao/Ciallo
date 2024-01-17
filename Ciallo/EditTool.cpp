#include "pch.hpp"
#include "EditTool.h"

#include <bitset>

#include "StrokeContainer.h"
#include "Stroke.h"
#include "Canvas.h"
#include "Brush.h"
#include "InnerBrush.h"
#include "Painter.h"
#include "ArrangementManager.h"
#include "BrushManager.h"
#include "SelectionManager.h"
#include "TimelineManager.h"

EditTool::EditTool()
{
}

void EditTool::OnClickOrDragStart(ClickOrDragStart event)
{
	if (!BezierDrawingMode)
	{
		std::array<float, 4> dists{};
		for (int i = 0; i < 4; ++i)
		{
			dists[i] = glm::distance(event.MousePos, Bone.Curve.ControlPoints[i]);
		}
		if (auto it = std::min_element(dists.begin(), dists.end()); *it < 0.001f)
		{
			DraggingControlPointIndex = std::distance(dists.begin(), it);
			return;
		}
		else
		{
			DraggingControlPointIndex = -1;
			Bone.Reset();
		}
		auto& SelectionTexture = R.ctx().get<SelectionManager>().SelectionTexture;
		SelectionTexture.BindFramebuffer();
		int x = static_cast<int>(event.MousePosPixel.x);
		int y = static_cast<int>(event.MousePosPixel.y);

		glm::vec4 clickedColor;
		glReadPixels(x, y, 1, 1, GL_RGBA, GL_FLOAT, &clickedColor);
		if (clickedColor == glm::vec4(0, 0, 0, 0))
		{
			SelectedStrokeE = entt::null;
			return;
		}

		uint32_t index = ColorToIndex(clickedColor);
		SelectedStrokeE = static_cast<entt::entity>(index);
		if (AutoBezierEdit)
		{
			Bone.Fit(SelectedStrokeE);
			Bone.Bind(SelectedStrokeE);
		}
		glBindFramebuffer(GL_FRAMEBUFFER, 0);
	}
	else
	{
		if (!FirstHandleDone)
		{
			Bone.Curve.ControlPoints = {event.MousePos, event.MousePos, event.MousePos, event.MousePos};
			Bone.UpdateOverlay();
			DrawingFirstHandle = true;
		}
		else
		{
			Bone.Curve.ControlPoints[2] = event.MousePos;
			Bone.Curve.ControlPoints[3] = event.MousePos;
			DrawingSecondHandle = true;
			FirstHandleDone = false;
		}
	}
}

void EditTool::OnDragging(Dragging event)
{
	if (!BezierDrawingMode)
	{
		if (DraggingControlPointIndex != -1)
		{
			Bone.Curve.ControlPoints[DraggingControlPointIndex] = event.MousePos;
			Bone.Update();
		}
		else if (SelectedStrokeE != entt::null && SelectedStrokeE != static_cast<entt::entity>(0))
		{
			auto& stroke = R.get<Stroke>(SelectedStrokeE);
			entt::entity currentDrawingE = R.ctx().get<TimelineManager>().GetCurrentDrawing();
			if(currentDrawingE == entt::null) return;
			for (auto& p : stroke.Position)
			{
				p += event.DeltaMousePos;
			}
			stroke.UpdateBuffers();
			auto usage = R.get<StrokeUsageFlags>(SelectedStrokeE);
			if (!!(usage & StrokeUsageFlags::Arrange))
				R.get<ArrangementManager>(currentDrawingE).AddOrUpdate(SelectedStrokeE);
			if (!!(usage & StrokeUsageFlags::Zone))
				R.get<ArrangementManager>(currentDrawingE).AddOrUpdateQuery(SelectedStrokeE);

			if (Bone.BoundStrokeE == SelectedStrokeE)
			{
				for (int i = 0; i < 4; ++i)
				{
					Bone.Curve.ControlPoints[i] += event.DeltaMousePos;
				}
				Bone.Curve.EvalLUT();
				Bone.UpdateOverlay();
				Bone.PrevCurve = Bone.Curve;
			}
		}
	}
	else
	{
		if (DrawingFirstHandle)
		{
			Bone.Curve.ControlPoints[1] = event.MousePos;
			Bone.Update();
		}

		if (DrawingSecondHandle)
		{
			Bone.Curve.ControlPoints[2] = event.MousePos;
			Bone.Update();
		}
	}
}

void EditTool::OnDragEnd(DragEnd event)
{
	if (BezierDrawingMode && DrawingFirstHandle)
	{
		Bone.Curve.ControlPoints[1] = event.MousePos;
		Bone.Update();
		DrawingFirstHandle = false;
		FirstHandleDone = true;
	}

	if (BezierDrawingMode && DrawingSecondHandle)
	{
		Bone.Curve.ControlPoints[2] = event.MousePos;
		Bone.Update();
		Bone.Bind(SelectedStrokeE);
		DrawingSecondHandle = false;
		BezierDrawingMode = false;
	}
}

void EditTool::Activate()
{

}

void EditTool::DrawProperties()
{
	ImGui::Checkbox("Auto Bezier Edit", &AutoBezierEdit);
	if (SelectedStrokeE != entt::null && SelectedStrokeE != static_cast<entt::entity>(0))
	{
		if (ImGui::Button("Bezier Edit"))
		{
			BezierDrawingMode = true;
			Bone.Reset();
		}
		auto& stroke = R.get<Stroke>(SelectedStrokeE);
		auto& strokeUsage = R.get<StrokeUsageFlags>(SelectedStrokeE);
		ImGui::TextUnformatted(fmt::format("number of vertices: {}", stroke.Position.size()).c_str());

		ImGui::CheckboxFlags("Final##0", reinterpret_cast<unsigned*>(&strokeUsage),
		                     static_cast<unsigned>(StrokeUsageFlags::Final));
		ImGui::CheckboxFlags("Fill##1", reinterpret_cast<unsigned*>(&strokeUsage),
		                     static_cast<unsigned>(StrokeUsageFlags::Fill));
		ImGui::CheckboxFlags("Arrange##2", reinterpret_cast<unsigned*>(&strokeUsage),
		                     static_cast<unsigned>(StrokeUsageFlags::Arrange));
		ImGui::CheckboxFlags("Zone##3", reinterpret_cast<unsigned*>(&strokeUsage),
		                     static_cast<unsigned>(StrokeUsageFlags::Zone));
		if (ImGui::Button("Edit Brush"))
			R.ctx().get<BrushManager>().OpenBrushEditor(&stroke.BrushE);
		ImGui::ColorEdit4("Line", glm::value_ptr(stroke.Color), ImGuiColorEditFlags_InputRGB);
		ImGui::ColorEdit4("Fill", glm::value_ptr(stroke.FillColor), ImGuiColorEditFlags_InputRGB);
		ImGui::DragFloat("Radius", &stroke.Radius, 0.0001f, 0.0001f, 0.030f, "%.4f",
		                 ImGuiSliderFlags_ClampOnInput);
		if (ImGui::Button("Remove Stroke") || ImGui::IsKeyPressed(ImGuiKey_X))
			RemoveSelectedStroke();
		if (ImGui::Button("CopyPaste Stroke") || ImGui::IsKeyPressed(ImGuiKey_C))
			CopyPasteSelectedStroke();
	}
	else
	{
		ImGui::TextWrapped("No stroke is selected, click on your target to select it, drag to move it");
	}
}

void EditTool::Deactivate()
{
	if (BezierDrawingMode) BezierDrawingMode = false;
	Bone.Reset();
}

void EditTool::OnHovering(Hovering event)
{
	if (BezierDrawingMode && FirstHandleDone)
	{
		Bone.Curve.ControlPoints[2] = event.MousePos;
		Bone.Curve.ControlPoints[3] = event.MousePos;
		Bone.Update();
	}
}

std::string EditTool::GetName()
{
	return "Edit";
}

glm::vec4 EditTool::IndexToColor(uint32_t index) const
{
	std::bitset<32> bits{index};
	glm::vec4 color;
	for (int i = 0; i < 4; ++i)
	{
		color[i] = std::bitset<8>(bits.to_string(), 32 - 8 * (i + 1), 8).to_ulong() / 255.f;
	}
	return color;
}

uint32_t EditTool::ColorToIndex(glm::vec4 color) const
{
	std::string bits;
	for (int i = 0; i < 4; ++i)
	{
		auto v = (uint32_t)glm::round(color[3 - i] * 255.f);
		std::bitset<8> b(v);
		bits += b.to_string();
	}
	return std::bitset<32>(bits).to_ulong();
}

void EditTool::RemoveSelectedStroke()
{
	if (SelectedStrokeE != entt::null && SelectedStrokeE != static_cast<entt::entity>(0))
	{
		auto& stroke = R.get<Stroke>(SelectedStrokeE);
		auto& strokeUsage = R.get<StrokeUsageFlags>(SelectedStrokeE);
		auto currentDrawingE = R.ctx().get<TimelineManager>().GetCurrentDrawing();
		if(currentDrawingE == entt::null) return;
		if (!!(strokeUsage & StrokeUsageFlags::Arrange))
			R.get<ArrangementManager>(currentDrawingE).Remove(SelectedStrokeE);
		if (!!(strokeUsage & StrokeUsageFlags::Zone))
			R.get<ArrangementManager>(currentDrawingE).RemoveQuery(SelectedStrokeE);
		
		auto& es = R.get<StrokeContainer>(currentDrawingE).StrokeEs;
		es.erase(std::find(es.begin(), es.end(), SelectedStrokeE));
		SelectedStrokeE = entt::null;
	}
}

void EditTool::CopyPasteSelectedStroke()
{
	if (SelectedStrokeE != entt::null && SelectedStrokeE != static_cast<entt::entity>(0))
	{
		auto& stroke = R.get<Stroke>(SelectedStrokeE);
		auto& strokeUsage = R.get<StrokeUsageFlags>(SelectedStrokeE);
		entt::entity newE = R.create();
		R.emplace<Stroke>(newE, stroke.Copy());
		R.emplace<StrokeUsageFlags>(newE, strokeUsage);
		auto currentDrawingE = R.ctx().get<TimelineManager>().GetCurrentDrawing();
		if (currentDrawingE == entt::null) return;
		if (!!(strokeUsage & StrokeUsageFlags::Arrange))
			R.get<ArrangementManager>(currentDrawingE).AddOrUpdate(newE);
		if (!!(strokeUsage & StrokeUsageFlags::Zone))
			R.get<ArrangementManager>(currentDrawingE).AddOrUpdateQuery(newE);

		auto& es = R.get<StrokeContainer>(currentDrawingE).StrokeEs;
		es.push_back(newE);
	}
}
