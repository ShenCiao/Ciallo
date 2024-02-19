#pragma once

#include "Viewport.h"
#include "RenderableTexture.h"

class Canvas
{
public:
	RenderableTexture Image{};
	entt::dispatcher EventDispatcher{};

	Canvas();

	float DrawingRotation = 0.0f;
	float Zoom = 1.0f;
	glm::vec2 Scroll{0.0f, 0.0f};
	Viewport Viewport{};
	float Dpi = 0.0f;

	void DrawUI();
	void GenRenderTarget();
	glm::ivec2 GetSizePixel() const;
	void Export() const;
	void Run();
private:
	ImGuiID InteractionButtonID;
	chrono::time_point<chrono::high_resolution_clock> StartDraggingTimePoint{};
	bool IsDragging = false;
	glm::vec2 PrevMousePos{}; // used for dragging
	glm::vec2 PrevMousePosPixel{};
};

