#pragma once

#include "Tool.h"
#include "Stroke.h"

class EditTool : public Tool
{
public:
	Stroke* SelectedStroke = nullptr;
	glm::vec2 MousePrev{};

	GLuint Texture = 0;
	GLuint FrameBuffer = 0;

	explicit EditTool(CanvasPanel* canvas);
	~EditTool();

	void ClickOrDragStart() override;
	void Dragging() override;
	void Activate() override;
	void DragEnd() override;
	void Deactivate() override;

	void GenRenderTargetFromActiveDrawing();
	void DestroyRenderTarget();
	void RenderTextureForSelection();

	glm::vec4 IndexToColor(uint32_t index);
	uint32_t ColorToIndex(glm::vec4 color);
};
