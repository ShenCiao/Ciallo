#pragma once

class CanvasPanel
{
	entt::entity mDrawing = entt::null;
	float mDrawingRotation = 0.0f;
	float mZoom = 1.0f;
	glm::vec2 mScroll{0.0f, 0.0f};

	void Update(entt::registry& r);
};
