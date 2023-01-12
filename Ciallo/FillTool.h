#pragma once

#include "Painter.h"
#include "Tool.h"
#include "CanvasEvent.h"

class FillTool : public Tool
{
private:
	Painter Painter{};
	void PadVisRim();
public:
	FillTool();

	std::string GetName() override;
	void OnClickOrDragStart(ClickOrDragStart) override;
	void OnDragging(Dragging) override;
	void Activate() override;
	void Deactivate() override;
	void DrawProperties() override;
	void OnHovering(Hovering) override;
};
