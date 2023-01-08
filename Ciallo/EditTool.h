#pragma once

#include "RenderableTexture.h"
#include "Tool.h"

class EditTool : public Tool
{
public:
	entt::entity SelectedStrokeE = entt::null;
	RenderableTexture SelectionTexture;

	EditTool();

	void OnClickOrDragStart(ClickOrDragStart) override;
	void OnDragging(Dragging) override;
	void OnDragEnd(DragEnd) override;
	void Activate() override;
	void Deactivate() override;
	void DrawProperties() override;

	void GenSelectionTexture();
	void RenderSelectionTexture();

	std::string GetName() override;

	glm::vec4 IndexToColor(uint32_t index) const;
	uint32_t ColorToIndex(glm::vec4 color) const;
};
