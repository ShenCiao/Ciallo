#include "pch.hpp"
#include "Drawing.h"

#include <glm/gtc/matrix_transform.hpp>

std::pair<int, int> Drawing::GetSizeInPixel() const
{
	float width = (LowerRight.x - UpperLeft.x) * Dpi / 0.0254f;
	float height = (LowerRight.y - UpperLeft.y) * Dpi / 0.0254f;
	return {static_cast<int>(glm::round(width)), static_cast<int>(glm::round(height))};
}

glm::vec2 Drawing::GetSize() const
{
	return LowerRight - UpperLeft;
}

void Drawing::GenRenderTarget()
{
	glCreateTextures(GL_TEXTURE_2D, 1, &Texture);
	glTextureParameteri(Texture, GL_TEXTURE_MIN_FILTER, GL_NEAREST);
	glTextureParameteri(Texture, GL_TEXTURE_MIN_FILTER, GL_NEAREST);
	glTextureParameteri(Texture, GL_TEXTURE_WRAP_S, GL_REPEAT);
	glTextureParameteri(Texture, GL_TEXTURE_WRAP_T, GL_REPEAT);
	glTextureStorage2D(Texture, 1, GL_RGBA8, GetSizeInPixel().first, GetSizeInPixel().second);

	glCreateFramebuffers(1, &FrameBuffer);
	glNamedFramebufferTexture(FrameBuffer, GL_COLOR_ATTACHMENT0, Texture, 0);
	if (glCheckNamedFramebufferStatus(FrameBuffer, GL_FRAMEBUFFER) != GL_FRAMEBUFFER_COMPLETE)
	{
		throw std::runtime_error("Framebuffer incomplete");
	}
	glBindFramebuffer(GL_FRAMEBUFFER, FrameBuffer);
	glClearColor(0.2f, 0.3f, 0.3f, 1.0f);
	glClear(GL_COLOR_BUFFER_BIT);
	glBindFramebuffer(GL_FRAMEBUFFER, 0);
}

glm::mat4 Drawing::GetViewProjMatrix() const
{
	return glm::ortho(UpperLeft.x, LowerRight.x, LowerRight.y, UpperLeft.y);
}
