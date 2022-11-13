#pragma once

#include "Drawing.h"
#include "PaintTool.h"
#include "EditTool.h"

class CanvasPanel
{
public:
	CanvasPanel();
	Drawing* ActiveDrawing = nullptr;
	float DrawingRotation = 0.0f;
	float Zoom = 1.0f;
	glm::vec2 Scroll{0.0f, 0.0f};
	glm::vec2 MousePosOnDrawing;
	glm::ivec2 MousePosOnDrawingInPixel;

	Tool* ActiveTool;
	std::unique_ptr<PaintTool> PaintTool;
	std::unique_ptr<EditTool> EditTool;

	void DrawAndRunTool();
};
