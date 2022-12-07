#include "pch.hpp"
#include "PaintTool.h"

#include "Stroke.h"
#include "CanvasPanel.h"

void PaintTool::ClickOrDragStart()
{
	auto s = std::make_unique<Stroke>();

	s->Position = { Canvas->MousePosOnDrawing };
	s->Thickness = {0.01f};
	s->Brush = ActiveBrush;
	s->OnChanged();
	Canvas->ActiveDrawing->ArrangementSystem.AddOrUpdate(s.get());
	Canvas->ActiveDrawing->Strokes.push_back(std::move(s));
	LastSampleDuration = chrono::duration<float, std::milli>::zero();
}

void PaintTool::Dragging()
{
	glm::vec2 delta = glm::abs(glm::vec2(ImGui::GetMousePos() - LastSampleMousePos));

	if (DraggingDuration - LastSampleDuration > SampleInterval && delta.x+delta.y >= 6.0f)
	{
		auto& s = Canvas->ActiveDrawing->Strokes.back();

		glm::vec2 pos = Canvas->MousePosOnDrawing;
		s->Position.push_back(pos);
		s->Thickness.emplace_back(0.01f);
		s->OnChanged();
		Canvas->ActiveDrawing->ArrangementSystem.AddOrUpdate(s.get());

		LastSampleMousePos = ImGui::GetMousePos();
		LastSampleDuration = DraggingDuration;
	}
}
