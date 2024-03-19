#include "pch.hpp"
#include "CanvasSelectionTextureManager.h"

#include <bitset>

#include "Canvas.h"
#include "StrokeContainer.h"
#include "InnerBrush.h"
#include "Stroke.h"
#include "TimelineManager.h"

CanvasSelectionTextureManager::CanvasSelectionTextureManager()
= default;

void CanvasSelectionTextureManager::GenSelectionTexture(glm::ivec2 size)
{
	SelectionTexture = RenderableTexture(size.x, size.y);
}

void CanvasSelectionTextureManager::RenderSelectionTexture()
{
	SelectionTexture.BindFramebuffer();
	glDisable(GL_BLEND);
	glDisable(GL_STENCIL_TEST);
	glDisable(GL_DEPTH_TEST);

	glClearColor(0, 0, 0, 0);
	glClear(GL_COLOR_BUFFER_BIT);

	entt::entity drawingE = R.ctx().get<TimelineManager>().GetRenderDrawing();
	auto& strokeEs = R.get<StrokeContainer>(drawingE).StrokeEs;
	auto& canvas = R.ctx().get<Canvas>();
	canvas.Viewport.UploadMVP();
	canvas.Viewport.BindMVPBuffer();
	auto& brush = R.ctx().get<InnerBrush>().Get("vanilla");
	brush.Use();
	for (auto& e : strokeEs)
	{
		brush.SetUniforms();
		auto& s = R.get<Stroke>(e);
		s.SetUniforms();
		glm::vec4 color = IndexToColor(static_cast<uint32_t>(e));
		glUniform4fv(1, 1, glm::value_ptr(color));
		s.LineDrawCall();
	}
	glBindFramebuffer(GL_FRAMEBUFFER, 0);
}

glm::vec4 CanvasSelectionTextureManager::IndexToColor(uint32_t index)
{
	std::bitset<32> bits{ index };
	glm::vec4 color;
	for (int i = 0; i < 4; ++i)
	{
		color[i] = std::bitset<8>(bits.to_string(), 32 - 8 * (i + 1), 8).to_ulong() / 255.f;
	}
	return color;
}

uint32_t CanvasSelectionTextureManager::ColorToIndex(glm::vec4 color)
{
	std::string bits;
	for (int i = 0; i < 4; ++i)
	{
		auto v = static_cast<uint32_t>(glm::round(color[3 - i] * 255.f));
		std::bitset<8> b(v);
		bits += b.to_string();
	}
	return std::bitset<32>(bits).to_ulong();
}