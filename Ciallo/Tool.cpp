#include "pch.hpp"
#include "Tool.h"

Tool::Tool(CanvasPanel* canvas): Canvas(canvas)
{
}

void Tool::Run()
{
	if (ImGui::IsMouseClicked(0) && ImGui::IsItemActivated())
	{
		if (IsDragging) IsDragging = false;
		StartDraggingTimePoint = chrono::high_resolution_clock::now();
		ClickOrDragStart();
		return;
	}

	if (ImGui::IsMouseDragging(0) && !ImGui::IsItemActivated() && ImGui::IsItemActive())
	{
		IsDragging = true;
		DraggingDuration = chrono::high_resolution_clock::now() - StartDraggingTimePoint;
		Dragging();
		return;
	}

	if (IsDragging && !ImGui::IsMouseDragging(0))
	{
		IsDragging = false;
		DragEnd();
		DraggingDuration = chrono::duration<float, std::milli>::zero();
		return;
	}

	if (ImGui::IsItemHovered() && !IsDragging && !ImGui::IsMouseClicked(0))
	{
		Hovering();
		return;
	}
}
