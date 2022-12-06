#include "pch.hpp"
#include "EditTool.h"

#include <glm/gtc/type_ptr.hpp>
#include <bitset>

#include "CanvasPanel.h"
#include "RenderingSystem.h"


EditTool::EditTool(CanvasPanel* canvas): Tool(canvas)
{
}

EditTool::~EditTool()
{
	DestroyRenderTarget();
}

void EditTool::ClickOrDragStart()
{
	glBindFramebuffer(GL_FRAMEBUFFER, FrameBuffer);
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
	GenRenderTargetFromActiveDrawing();
	RenderTextureForSelection();
}

void EditTool::DragEnd()
{
	RenderTextureForSelection();
}

void EditTool::Deactivate()
{
	DestroyRenderTarget();
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

void EditTool::GenRenderTargetFromActiveDrawing()
{
	// Create textures used for selection
	glCreateTextures(GL_TEXTURE_2D, 1, &Texture);
	glTextureParameteri(Texture, GL_TEXTURE_MIN_FILTER, GL_LINEAR);
	glTextureParameteri(Texture, GL_TEXTURE_MAG_FILTER, GL_LINEAR);
	glTextureParameteri(Texture, GL_TEXTURE_WRAP_S, GL_CLAMP_TO_EDGE);

	glTextureParameteri(Texture, GL_TEXTURE_WRAP_T, GL_CLAMP_TO_EDGE);

	Drawing* drawing = Canvas->ActiveDrawing;
	glTextureStorage2D(Texture, 1, GL_RGBA8, drawing->GetSizeInPixel().x, drawing->GetSizeInPixel().y);

	glCreateFramebuffers(1, &FrameBuffer);
	glNamedFramebufferTexture(FrameBuffer, GL_COLOR_ATTACHMENT0, Texture, 0);

	if (glCheckNamedFramebufferStatus(FrameBuffer, GL_FRAMEBUFFER) != GL_FRAMEBUFFER_COMPLETE)
	{
		throw std::runtime_error("Framebuffer incomplete");
	}
}

void EditTool::DestroyRenderTarget()
{
	glDeleteTextures(1, &Texture);
	glDeleteFramebuffers(1, &FrameBuffer);
}

void EditTool::RenderTextureForSelection()
{
	glBindFramebuffer(GL_FRAMEBUFFER, FrameBuffer);
	glClearColor(1, 1, 1, 1);
	glClear(GL_COLOR_BUFFER_BIT);
	glDisable(GL_CULL_FACE);
	glDisable(GL_BLEND);

	Drawing* drawing = Canvas->ActiveDrawing;
	auto pixelSize = drawing->GetSizeInPixel();
	glViewport(0, 0, pixelSize.x, pixelSize.y);
	glUseProgram(RenderingSystem::ArticulatedLine->Program());
	glm::mat4 mvp = drawing->GetViewProjMatrix();
	glUniformMatrix4fv(0, 1, GL_FALSE, glm::value_ptr(mvp)); // mvp

	int index = 0;
	for (auto& s : drawing->Strokes)
	{
		glm::vec4 color = IndexToColor(index);
		glUniform4fv(1, 1, glm::value_ptr(color));

		s->Draw();
		index += 1;
	}

	glUseProgram(0);
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
