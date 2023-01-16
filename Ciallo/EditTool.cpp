#include "pch.hpp"
#include "EditTool.h"

#include <bitset>

#include "StrokeContainer.h"
#include "Stroke.h"
#include "Canvas.h"
#include "Brush.h"
#include "InnerBrush.h"
#include "Painter.h"
#include "ArrangementManager.h"


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
	if (SelectedStrokeE != entt::null && SelectedStrokeE != static_cast<entt::entity>(0))
	{
		auto& stroke = R.get<Stroke>(SelectedStrokeE);
		for (auto& p : stroke.Position)
		{
			p = {p.x + event.DeltaMousePos.x, p.y + event.DeltaMousePos.y};
		}
		stroke.UpdateBuffers();
		R.ctx().get<ArrangementManager>().AddOrUpdate(SelectedStrokeE);
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

void EditTool::DrawProperties()
{
	if (SelectedStrokeE != entt::null && SelectedStrokeE != static_cast<entt::entity>(0))
	{
		auto& stroke = R.get<Stroke>(SelectedStrokeE);
		auto& strokeUsage = R.get<StrokeUsageFlags>(SelectedStrokeE);
		ImGui::TextUnformatted(fmt::format("number of vertices: {}", stroke.Position.size()).c_str());
		
		ImGui::CheckboxFlags("Label##0", reinterpret_cast<unsigned*>(&strokeUsage),
		                     static_cast<unsigned>(StrokeUsageFlags::Final));
		ImGui::CheckboxFlags("Fill##1", reinterpret_cast<unsigned*>(&strokeUsage),
		                     static_cast<unsigned>(StrokeUsageFlags::Fill));
		ImGui::CheckboxFlags("Arrange##2", reinterpret_cast<unsigned*>(&strokeUsage),
		                     static_cast<unsigned>(StrokeUsageFlags::Arrange));
		ImGui::CheckboxFlags("Zone##3", reinterpret_cast<unsigned*>(&strokeUsage),
		                     static_cast<unsigned>(StrokeUsageFlags::Zone));
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
	glDisable(GL_BLEND);
	glDisable(GL_STENCIL_TEST);
	glDisable(GL_DEPTH_TEST);
	auto& strokeEs = R.ctx().get<StrokeContainer>().StrokeEs;
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
