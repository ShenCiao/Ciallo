#pragma once

#include "Drawing.h"
#include "PaintTool.h"

class CanvasPanel
{
public:
	CanvasPanel();
	Drawing* ActiveDrawing = nullptr;
	float DrawingRotation = 0.0f;
	float Zoom = 1.0f;
	glm::vec2 Scroll{0.0f, 0.0f};
	glm::vec2 MousePosOnDrawing;

	Tool* ActiveTool;
	std::unique_ptr<PaintTool> PaintTool;

	void DrawAndRunTool();
};
