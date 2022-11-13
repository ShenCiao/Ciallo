#include "pch.hpp"
#include "EditTool.h"

#include "CanvasPanel.h"

void EditTool::ClickOrDragStart()
{
	SelectedStroke = Canvas->ActiveDrawing->Strokes.back().get();
	MousePrev = Canvas->MousePosOnDrawing;
}

void EditTool::Dragging()
{
	glm::vec2 delta = Canvas->MousePosOnDrawing - MousePrev;

	for (auto& p : SelectedStroke->Position)
	{
		p = { p.x() + delta.x, p.y() + delta.y };
	}
	SelectedStroke->OnChanged();
	MousePrev = Canvas->MousePosOnDrawing;
}
