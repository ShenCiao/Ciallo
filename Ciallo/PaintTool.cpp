#include "pch.hpp"
#include "PaintTool.h"

#include "BrushManager.h"

PaintTool::PaintTool()
{
	auto& brushM = R.ctx().get<BrushManager>();
	Painter.BrushE = brushM.Brushes[2];
	Painter.Usage = StrokeUsageFlags::Arrange | StrokeUsageFlags::Final;
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
	Painter.DrawProperties();
}
