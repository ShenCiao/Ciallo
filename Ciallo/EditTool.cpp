#include "pch.hpp"
#include "EditTool.h"

#include <bitset>

#include "CanvasPanel.h"


EditTool::EditTool(CanvasPanel* canvas): Tool(canvas)
{
}

void EditTool::ClickOrDragStart()
{
	SelectionTexture.BindFramebuffer();
	int x = Canvas->MousePosOnDrawingInPixel.x;
	int y = Canvas->MousePosOnDrawingInPixel.y;
	glm::vec4 clickedColor;
	glReadPixels(x, y, 1, 1, GL_RGBA, GL_FLOAT, &clickedColor);
	if (clickedColor == glm::vec4(1, 1, 1, 1))
	{
		SelectedStroke = nullptr;
		return;
	}

	uint32_t index = ColorToIndex(clickedColor);
	SelectedStroke = Canvas->ActiveDrawing->Strokes[index].get();
	glBindFramebuffer(GL_FRAMEBUFFER, 0);
	MousePrev = Canvas->MousePosOnDrawing;
}

void EditTool::Dragging()
{
	if(SelectedStroke)
	{
		glm::vec2 delta = Canvas->MousePosOnDrawing - MousePrev;

		for (auto& p : SelectedStroke->Position)
		{
			p = { p.x + delta.x, p.y + delta.y };
		}
		SelectedStroke->OnChanged();
		Canvas->ActiveDrawing->ArrangementSystem.AddOrUpdate(SelectedStroke);
		MousePrev = Canvas->MousePosOnDrawing;
	}
}

void EditTool::Activate()
{
	GenSelectionTexture();
	RenderTextureForSelection();
}

void EditTool::DragEnd()
{
	RenderTextureForSelection();
}

void EditTool::Deactivate()
{
	
}

void EditTool::DrawProperties()
{
	if(SelectedStroke != nullptr)
	{
		if (ImGui::Button("Size"))
		{
			spdlog::info("size: {}", SelectedStroke->Position.size());
		}
	}
}

void EditTool::GenSelectionTexture()
{
	// Create textures used for selection
	glm::ivec2 size = Canvas->ActiveDrawing->GetSizeInPixel();
	SelectionTexture = RenderableTexture(size.x, size.y);
}
void EditTool::RenderTextureForSelection()
{
	Drawing* drawing = Canvas->ActiveDrawing;
	SelectionTexture.BindFramebuffer();
	int index = 0;
	for (auto& s : drawing->Strokes)
	{
		// VanillaBrush->Use();
		// VanillaBrush->SetUniform();
		s->SetUniform();
		glm::vec4 color = IndexToColor(index);
		glUniform4fv(1, 1, glm::value_ptr(color));
		s->DrawCall();
		index += 1;
	}
	glBindFramebuffer(GL_FRAMEBUFFER, 0);
}

glm::vec4 EditTool::IndexToColor(uint32_t index)
{
	std::bitset<32> bits{index};
	glm::vec4 color;
	for (int i = 0; i < 4; ++i)
	{
		color[i] = std::bitset<8>(bits.to_string(), 32 - 8 * (i + 1), 8).to_ulong() / 255.f;
	}
	return color;
}

uint32_t EditTool::ColorToIndex(glm::vec4 color)
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
