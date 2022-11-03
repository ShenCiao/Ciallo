#pragma once

class Drawing : EntityObject
{
public:
	GLuint Texture = 0;
	GLuint FrameBuffer = 0;

	glm::vec2 UpperLeft = { 0.0f, 0.0f };
	glm::vec2 LowerRight{};
	float Dpi = 0.0f;

	std::pair<int, int> GetSizeInPixel() const;
	glm::vec2 GetSize() const;
	void GenRenderTarget();
	void DeleteRenderTarget();
	glm::mat4 GetViewProjMatrix() const;
};
