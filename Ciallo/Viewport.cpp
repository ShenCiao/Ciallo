#include "pch.hpp"
#include "Viewport.h"

Viewport::Viewport()
{
	glCreateBuffers(1, &MVPBuffer);
	glNamedBufferStorage(MVPBuffer, sizeof(glm::mat4), nullptr, GL_DYNAMIC_STORAGE_BIT);
}

Viewport::Viewport(glm::vec2 min, glm::vec2 max): Min(min),
                                                  Max(max)
{
	glCreateBuffers(1, &MVPBuffer);
	glNamedBufferStorage(MVPBuffer, sizeof(glm::mat4), nullptr, GL_DYNAMIC_STORAGE_BIT);
}

Viewport::~Viewport()
{
	glDeleteBuffers(1, &MVPBuffer);
}

Viewport::Viewport(const Viewport& other): Min(other.Min),
                                           Max(other.Max)
{
	glCreateBuffers(1, &MVPBuffer);
	glNamedBufferStorage(MVPBuffer, sizeof(glm::mat4), nullptr, GL_DYNAMIC_STORAGE_BIT);
}

Viewport::Viewport(Viewport&& other) noexcept:
	Min(other.Min),
	Max(other.Max),
	MVPBuffer(other.MVPBuffer)
{
	other.MVPBuffer = 0;
}

Viewport& Viewport::operator=(Viewport other)
{
	using std::swap;
	swap(*this, other);
	return *this;
}

glm::mat4 Viewport::GetViewProjMatrix() const
{
	return glm::ortho(Min.x, Max.x, Min.y, Max.y);
}

GLuint Viewport::GenMVPBuffer(glm::mat4 model)
{
	glm::mat4 mvp = model * GetViewProjMatrix();
	glNamedBufferSubData(MVPBuffer, 0, sizeof(glm::mat4), &mvp);
	return MVPBuffer;
}

void Viewport::BindMVPBuffer() const
{
	glBindBufferBase(GL_UNIFORM_BUFFER, 0, MVPBuffer); 
}

void swap(Viewport& lhs, Viewport& rhs) noexcept
{
	using std::swap;
	swap(lhs.Min, rhs.Min);
	swap(lhs.Max, rhs.Max);
	swap(lhs.MVPBuffer, rhs.MVPBuffer);
}
