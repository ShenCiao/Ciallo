#include "pch.hpp"
#include "PaintTool.h"

#include "Stroke.h"
#include "CanvasPanel.h"

void PaintTool::ClickOrDragStart()
{
	auto s = std::make_unique<Stroke>();
 	glm::vec2 pos = Panel->MousePosOnDrawing;
	
	s->Position = { {pos.x, pos.y} };
	s->Width = { 0.001f };
	s->OnChanged();
	Panel->ActiveDrawing->Strokes.push_back(std::move(s));
}
