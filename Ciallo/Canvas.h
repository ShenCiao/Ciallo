#pragma once

#include "Viewport.h"
#include "RenderableTexture.h"

class Canvas
{
public:
	RenderableTexture Image{};
	entt::dispatcher EventDispatcher;

	float DrawingRotation = 0.0f;
	float Zoom = 1.0f;
	glm::vec2 Scroll{0.0f, 0.0f};
	Viewport Viewport{};
	float Dpi = 0.0f;

	void DrawUI();
	void GenRenderTarget();
	void Render();
	glm::ivec2 GetSizePixel() const;
private:
	chrono::time_point<chrono::high_resolution_clock> StartDraggingTimePoint{};
	bool IsDragging = false;
	glm::vec2 PrevMousePos{}; // used for dragging
};

