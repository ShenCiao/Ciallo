#include "pch.hpp"
#include "FillTool.h"

#include "Stroke.h"
#include "CanvasPanel.h"

void FillTool::ClickOrDragStart()
{
	// Changed part: Width, Label, remove Arrangement
	auto s = std::make_unique<Stroke>();

	s->Position = { Canvas->MousePosOnDrawing };
	s->Width = { 0.0003f };
	s->OnChanged();
	Canvas->ActiveDrawing->ArrangementSystem.AddOrUpdateQuery(s.get());
	Canvas->ActiveDrawing->Labels.push_back(std::move(s));
	LastSampleDuration = chrono::duration<float, std::milli>::zero();
}

void FillTool::Dragging()
{
	// Changed part: Width, Label, delta threshold, SampleInterval
	glm::vec2 delta = glm::abs(glm::vec2(ImGui::GetMousePos() - LastSampleMousePos));

	if (DraggingDuration - LastSampleDuration > SampleInterval && delta.x + delta.y >= 6.0f)
	{
		auto& s = Canvas->ActiveDrawing->Labels.back();

		glm::vec2 pos = Canvas->MousePosOnDrawing;
		s->Position.emplace_back(pos);
		s->Width.emplace_back(0.0003f);
		s->OnChanged();
		Canvas->ActiveDrawing->ArrangementSystem.AddOrUpdateQuery(s.get());
		LastSampleMousePos = ImGui::GetMousePos();
		LastSampleDuration = DraggingDuration;
	}
}
