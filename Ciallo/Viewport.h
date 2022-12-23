#pragma once


class Viewport
{
public:
	glm::vec2 Min{}, Max{};
	GLuint MVPBuffer = 0;

	Viewport();
	Viewport(glm::vec2 min, glm::vec2 max);
	~Viewport();
	Viewport(const Viewport& other);
	Viewport(Viewport&& other) noexcept;
	Viewport& operator=(Viewport other);

	void GenBuffer();
	friend void swap(Viewport& lhs, Viewport& rhs) noexcept;

	glm::mat4 GetViewProjMatrix() const;
	GLuint UploadMVP(glm::mat4 model = glm::identity<glm::mat4>());
	void BindMVPBuffer() const;
	glm::vec2 GetSizePixelFloat(float dpi) const;
	glm::ivec2 GetSizePixel(float dpi) const;
};
