#pragma once

#include "RenderableTexture.h"
#include "Tool.h"
#include "CubicBezierBone.h"

class EditTool : public Tool
{
	entt::entity SelectedStrokeE = entt::null;
	void GenSelectionTexture();
	void RenderSelectionTexture();

	bool BezierCustomizeMode = false; // Shitty design, change it later.
	CubicBezierBone Bone;
public:
	RenderableTexture SelectionTexture;

	EditTool();

	void OnClickOrDragStart(ClickOrDragStart) override;
	void OnDragging(Dragging) override;
	void OnDragEnd(DragEnd) override;
	void Activate() override;
	void DrawProperties() override;

	std::string GetName() override;

	glm::vec4 IndexToColor(uint32_t index) const;
	uint32_t ColorToIndex(glm::vec4 color) const;
};
