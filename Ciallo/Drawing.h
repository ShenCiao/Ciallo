#pragma once

#include "Stroke.h"
#include "ArrangementManager.h"

class Drawing
{
public:
	GLuint ColorTexture = 0;
	GLuint DepthStencilTexture = 0;
	GLuint FrameBuffer = 0;

	glm::vec2 UpperLeft = { 0.0f, 0.0f };
	glm::vec2 LowerRight{};
	float Dpi = 0.0f;

	std::vector<std::unique_ptr<Stroke>> Strokes;
	ArrangementManager ArrangementSystem;
	std::vector<std::unique_ptr<Stroke>> Labels;

	Drawing();
	Drawing(const Drawing& other) = delete;
	Drawing(Drawing&& other) noexcept = delete;
	Drawing& operator=(const Drawing& other) = delete;
	Drawing& operator=(Drawing&& other) noexcept = delete;
	~Drawing();

	glm::ivec2 GetSizeInPixel() const;
	glm::vec2 GetSizeInPixelFloat() const;
	glm::vec2 GetWorldSize() const;
	void GenRenderTarget();
	void DeleteRenderTarget();
	glm::mat4 GetViewProjMatrix() const;
};
