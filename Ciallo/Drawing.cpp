#include "pch.hpp"
#include "Drawing.h"

#include <stb_image.h>
#include <glm/gtc/matrix_transform.hpp>

#include "RenderingSystem.h"

Drawing::Drawing()
{
	
}

Drawing::~Drawing()
{
	DeleteRenderTarget();
}

glm::ivec2 Drawing::GetSizeInPixel() const
{
	float width = (LowerRight.x - UpperLeft.x) * Dpi / 0.0254f;
	float height = (LowerRight.y - UpperLeft.y) * Dpi / 0.0254f;
	return {static_cast<int>(glm::round(width)), static_cast<int>(glm::round(height))};
}

glm::vec2 Drawing::GetSizeInPixelFloat() const
{
	float width = (LowerRight.x - UpperLeft.x) * Dpi / 0.0254f;
	float height = (LowerRight.y - UpperLeft.y) * Dpi / 0.0254f;
	return {width, height};
}

glm::vec2 Drawing::GetWorldSize() const
{
	return LowerRight - UpperLeft;
}

void Drawing::GenRenderTarget()
{
	int width = GetSizeInPixel().x;
	int height = GetSizeInPixel().y;
	// color buffer
	glCreateTextures(GL_TEXTURE_2D, 1, &ColorTexture);
	glTextureParameteri(ColorTexture, GL_TEXTURE_MIN_FILTER, GL_NEAREST);
	glTextureParameteri(ColorTexture, GL_TEXTURE_MAG_FILTER, GL_NEAREST);
	glTextureParameteri(ColorTexture, GL_TEXTURE_WRAP_S, GL_CLAMP_TO_EDGE);
	glTextureParameteri(ColorTexture, GL_TEXTURE_WRAP_T, GL_CLAMP_TO_EDGE);

	glTextureStorage2D(ColorTexture, 1, GL_RGBA8, width, height);

	// stencil and depth
	glCreateTextures(GL_TEXTURE_2D, 1, &DepthStencilTexture);
	glTextureParameteri(DepthStencilTexture, GL_TEXTURE_MIN_FILTER, GL_NEAREST);
	glTextureParameteri(DepthStencilTexture, GL_TEXTURE_MAG_FILTER, GL_NEAREST);
	glTextureParameteri(DepthStencilTexture, GL_TEXTURE_WRAP_S, GL_CLAMP_TO_EDGE);
	glTextureParameteri(DepthStencilTexture, GL_TEXTURE_WRAP_T, GL_CLAMP_TO_EDGE);

	glTextureStorage2D(DepthStencilTexture, 1, GL_DEPTH24_STENCIL8, width, height);

	glCreateFramebuffers(1, &FrameBuffer);
	glNamedFramebufferTexture(FrameBuffer, GL_COLOR_ATTACHMENT0, ColorTexture, 0);
	glNamedFramebufferTexture(FrameBuffer, GL_DEPTH_STENCIL_ATTACHMENT, DepthStencilTexture, 0);

	if (glCheckNamedFramebufferStatus(FrameBuffer, GL_FRAMEBUFFER) != GL_FRAMEBUFFER_COMPLETE)
	{
		throw std::runtime_error("Framebuffer incomplete");
	}
}

void Drawing::DeleteRenderTarget()
{
	glDeleteTextures(1, &ColorTexture);
	glDeleteFramebuffers(1, &FrameBuffer);
}

glm::mat4 Drawing::GetViewProjMatrix() const
{
	return glm::ortho(UpperLeft.x, LowerRight.x, UpperLeft.y, LowerRight.y);
}

void Drawing::Draw()
{
	glBindFramebuffer(GL_FRAMEBUFFER, FrameBuffer);
	glClearColor(1, 1, 1, 1);
	glClear(GL_COLOR_BUFFER_BIT | GL_STENCIL_BUFFER_BIT | GL_DEPTH_BUFFER_BIT);
	glDisable(GL_DEPTH_TEST);
	glDisable(GL_STENCIL_TEST);
	glDisable(GL_CULL_FACE);
	glEnable(GL_BLEND);
	glBlendFunc(GL_SRC_ALPHA, GL_ONE_MINUS_SRC_ALPHA);

	auto pixelSize = GetSizeInPixel();
	glViewport(0, 0, pixelSize.x, pixelSize.y);
	glUseProgram(RenderingSystem::ArticulatedLine->Program);
	glm::mat4 mvp = GetViewProjMatrix();
	glUniformMatrix4fv(0, 1, GL_FALSE, glm::value_ptr(mvp)); // mvp
	glUniform4f(1, 0.5, 0.5, 0.5, 1);// color

	for (auto& s : Strokes)
	{
		s->Draw();
	}

	for (auto& s : Labels)
	{
		s->Draw();
	}

	glUseProgram(0);

	// Filled regions
	glBindFramebuffer(GL_FRAMEBUFFER, FrameBuffer);
	glEnable(GL_BLEND);
	glEnable(GL_STENCIL_TEST);
	glDisable(GL_DEPTH_TEST);
	glUseProgram(RenderingSystem::Polygon->Program);
	glUniformMatrix4fv(0, 1, GL_FALSE, glm::value_ptr(mvp)); // mvp
	glUniform4f(1, 0.f, 0.f, 0.f, 0.3f); // color


	for (auto& [stroke, faces] : ArrangementSystem.QueryResultsContainer)
	{
		glUniform4fv(1, 1, glm::value_ptr(stroke->PolygonColor));
		for (ColorFace& face : faces)
		{
			face.GenUploadBuffers();
			face.Draw();
		}
	}

	glBindFramebuffer(GL_FRAMEBUFFER, 0);
}
