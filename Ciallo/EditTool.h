#pragma once

#include "RenderableTexture.h"
#include "Tool.h"
#include "CubicBezierBone.h"

class EditTool : public Tool
{
	bool AutoBezierEdit = true;

	bool BezierDrawingMode = false; // Shitty design, suppose to be a state mechine, rework for further change.
	bool DrawingFirstHandle = false;
	bool FirstHandleDone = false;
	bool DrawingSecondHandle = false;

	int DraggingControlPointIndex = -1;
	CubicBezierBone Bone;
public:
	EditTool();

	void OnClickOrDragStart(ClickOrDragStart) override;
	void OnDragging(Dragging) override;
	void OnDragEnd(DragEnd) override;
	void Activate() override;
	void DrawProperties() override;
	void Deactivate() override;
	void OnHovering(Hovering) override;

	std::string GetName() override;

private:
	entt::entity SelectedStrokeE = entt::null;
	glm::vec4 IndexToColor(uint32_t index) const;
	uint32_t ColorToIndex(glm::vec4 color) const;
};
