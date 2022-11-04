#pragma once

class Drawing : EntityObject
{
public:
	GLuint Texture = 0;
	GLuint FrameBuffer = 0;

	glm::vec2 UpperLeft = { 0.0f, 0.0f };
	glm::vec2 LowerRight{};
	float Dpi = 0.0f;

	glm::ivec2 GetSizeInPixel() const;
	glm::vec2 GetSizeInPixelFloat() const;
	glm::vec2 GetWorldSize() const;
	void GenRenderTarget();
	void DeleteRenderTarget();
	glm::mat4 GetViewProjMatrix() const;
};
