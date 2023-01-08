// #pragma once
//
// #include "Drawing.h"
// #include "PaintTool.h"
// #include "EditTool.h"
// #include "FillTool.h"
//
// class CanvasPanel
// {
// public:
// 	CanvasPanel();
// 	~CanvasPanel();
// 	Drawing* ActiveDrawing = nullptr;
// 	float DrawingRotation = 0.0f;
// 	float Zoom = 1.0f;
// 	glm::vec2 Scroll{0.0f, 0.0f};
// 	glm::vec2 MousePosOnDrawing;
// 	glm::ivec2 MousePosOnDrawingInPixel;
//
// 	/*
// 	 * Bad design to have a dedicate overlay buffer.
// 	 * It supposes to have several layers (which prevent users to edit) on the top to draw overlay on.
// 	 * Change it when reworking.
// 	 */
// 	GLuint OverlayColorTexture = 0;
// 	GLuint OverlayDepthStencilTexture = 0;
// 	GLuint OverlayFrameBuffer = 0;
//
//
// 	Tool* ActiveTool;
// 	std::unique_ptr<PaintTool> PaintTool;
// 	std::unique_ptr<EditTool> EditTool;
// 	std::unique_ptr<FillTool> FillTool;
//
// 	void GenOverlayBuffers();
// 	void DeleteOverlayBuffers();
// 	void DrawAndRunTool();
// };
