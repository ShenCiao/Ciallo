#pragma once

#include "Tool.h"
#include "Painter.h"

class PaintTool : public Tool
{
public:
	Painter Painter{};

	PaintTool();

	void OnClickOrDragStart(ClickOrDragStart) override;
	void OnDragging(Dragging) override;
	std::string GetName() override;
};
