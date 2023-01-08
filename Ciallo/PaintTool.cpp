#include "pch.hpp"
#include "PaintTool.h"

#include "BrushManager.h"

PaintTool::PaintTool()
{
	auto& brushM = R.ctx().get<BrushManager>();
	Painter.Brush = brushM.Brushes[2];
	Painter.Usage = StrokeUsageFlags::Arrange;
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
