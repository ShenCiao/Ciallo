#pragma once

#include "Tool.h"
#include "Stroke.h"

class EditTool : public Tool
{
	Stroke* SelectedStroke;
	glm::vec2 MousePrev;

	
public:
	explicit EditTool(CanvasPanel* panel)
		: Tool(panel)
	{
	}

	void ClickOrDragStart() override;
	void Dragging() override;

	void RenderSelection();
};
