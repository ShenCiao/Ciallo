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

	friend void swap(Viewport& lhs, Viewport& rhs) noexcept;

	glm::mat4 GetViewProjMatrix() const;
	GLuint GenMVPBuffer(glm::mat4 model = glm::identity<glm::mat4>());
	void BindMVPBuffer() const;
};
