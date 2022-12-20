#pragma once

#include "Tool.h"
#include "Stroke.h"
#include "RenderableTexture.h"

class EditTool : public Tool
{
public:
	entt::entity VanillaBrush; // warning: this field is uninited!!!
	Stroke* SelectedStroke = nullptr;
	glm::vec2 MousePrev{};

	RenderableTexture SelectionTexture;

	explicit EditTool(CanvasPanel* canvas);

	void ClickOrDragStart() override;
	void Dragging() override;
	void Activate() override;
	void DragEnd() override;
	void Deactivate() override;
	void DrawProperties() override;

	void GenSelectionTexture();
	void RenderTextureForSelection();

	glm::vec4 IndexToColor(uint32_t index);
	uint32_t ColorToIndex(glm::vec4 color);
};
