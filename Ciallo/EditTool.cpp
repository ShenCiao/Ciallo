#include "pch.hpp"
#include "EditTool.h"

#include <bitset>

#include "StrokeContainer.h"
#include "Stroke.h"
#include "Canvas.h"
#include "Brush.h"
#include "InnerBrush.h"


EditTool::EditTool()
{
}

void EditTool::OnClickOrDragStart(ClickOrDragStart event)
{
	SelectionTexture.BindFramebuffer();
	int x = static_cast<int>(event.MousePosPixel.x);
	int y = static_cast<int>(event.MousePosPixel.y);

	glm::vec4 clickedColor;
	glReadPixels(x, y, 1, 1, GL_RGBA, GL_FLOAT, &clickedColor);
	if (clickedColor == glm::vec4(1, 1, 1, 1))
	{
		SelectedStrokeE = entt::null;
		return;
	}

	uint32_t index = ColorToIndex(clickedColor);
	SelectedStrokeE = static_cast<entt::entity>(index);
	glBindFramebuffer(GL_FRAMEBUFFER, 0);
}

void EditTool::OnDragging(Dragging event)
{
	if (SelectedStrokeE != entt::null)
	{
		// for (auto& p : SelectedStroke->Position)
		// {
		// 	p = { p.x + delta.x, p.y + delta.y };
		// }
		// SelectedStroke->UpdateBuffers();
		// Canvas->ActiveDrawing->ArrangementSystem.AddOrUpdate(SelectedStroke);
	}
}

void EditTool::OnDragEnd(DragEnd)
{
	RenderSelectionTexture();
}

void EditTool::Activate()
{
	GenSelectionTexture();
	RenderSelectionTexture();
}

void EditTool::Deactivate()
{
}

void EditTool::DrawProperties()
{
	if (SelectedStrokeE != entt::null)
	{
		if (ImGui::Button("Size"))
		{
			auto& stroke = R.get<Stroke>(SelectedStrokeE);
			spdlog::info("size: {}", stroke.Position.size());
		}
	}
}

void EditTool::GenSelectionTexture()
{
	// Create textures used for selection
	auto& canvas = R.ctx().get<Canvas>();

	glm::ivec2 size = canvas.GetSizePixel();
	SelectionTexture = RenderableTexture(size.x, size.y);
}

void EditTool::RenderSelectionTexture()
{
	SelectionTexture.BindFramebuffer();

	auto& strokeEs = R.ctx().get<StrokeContainer>().StrokeEs;
	auto& canvas = R.ctx().get<Canvas>();
	canvas.Viewport.UploadMVP();
	canvas.Viewport.BindMVPBuffer();
	auto& brush = R.ctx().get<InnerBrush>().Get("vanilla");
	brush.Use();
	brush.SetUniforms();
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

std::string EditTool::GetName()
{
	return "Edit";
}

glm::vec4 EditTool::IndexToColor(uint32_t index) const
{
	std::bitset<32> bits{index};
	glm::vec4 color;
	for (int i = 0; i < 4; ++i)
	{
		color[i] = std::bitset<8>(bits.to_string(), 32 - 8 * (i + 1), 8).to_ulong() / 255.f;
	}
	return color;
}

uint32_t EditTool::ColorToIndex(glm::vec4 color) const
{
	std::string bits;
	for (int i = 0; i < 4; ++i)
	{
		auto v = (uint32_t)glm::round(color[3 - i] * 255.f);
		std::bitset<8> b(v);
		bits += b.to_string();
	}
	return std::bitset<32>(bits).to_ulong();
}
