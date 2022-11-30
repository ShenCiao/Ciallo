#include "pch.hpp"
#include "Stroke.h"

#include "RenderingSystem.h"

Stroke::Stroke()
{
	GenBuffers();
}

Stroke::~Stroke()
{
	glDeleteBuffers(VertexBuffers.size(), VertexBuffers.data());
	glDeleteBuffers(1, &VertexArray);
}

void Stroke::OnChanged()
{
	UpdatePositionBuffer();
	UpdateWidthBuffer();
	UpdateDistanceBuffer();
}

void Stroke::GenBuffers()
{
	glCreateVertexArrays(1, &VertexArray);
	glCreateBuffers(VertexBuffers.size(), VertexBuffers.data());

	glEnableVertexArrayAttrib(VertexArray, 0);
	glVertexArrayAttribBinding(VertexArray, 0, 0);
	glVertexArrayAttribFormat(VertexArray, 0, 2, GL_FLOAT, GL_FALSE, 0);
	glEnableVertexArrayAttrib(VertexArray, 1);
	glVertexArrayAttribBinding(VertexArray, 1, 1);
	glVertexArrayAttribFormat(VertexArray, 1, 1, GL_FLOAT, GL_FALSE, 0);

	GLintptr offsets[] = { 0, 0 };
	int strides[] = { sizeof(glm::vec2), sizeof(float) };

	glVertexArrayVertexBuffers(VertexArray, 0, VertexBuffers.size(), VertexBuffers.data(), offsets, strides);
}

void Stroke::UpdatePositionBuffer()
{
	glNamedBufferData(VertexBuffers[0], Position.size() * sizeof(glm::vec2), Position.data(), GL_DYNAMIC_DRAW);
}

void Stroke::UpdateWidthBuffer()
{
	glNamedBufferData(VertexBuffers[1], Width.size() * sizeof(float), Width.data(), GL_DYNAMIC_DRAW);
}

// Using position buffer, be careful about the calling order.
void Stroke::UpdateDistanceBuffer()
{
	glNamedBufferData(VertexBuffers[2], Position.size() * sizeof(float), nullptr, GL_DYNAMIC_DRAW);
	glUseProgram(RenderingSystem::PrefixSum->Program);
	glBindBufferBase(GL_SHADER_STORAGE_BUFFER, 0, VertexBuffers[0]);
	glBindBufferBase(GL_SHADER_STORAGE_BUFFER, 1, VertexBuffers[2]);
	glDispatchCompute(1, 1, 1);
}

void Stroke::Draw()
{
	int count = Position.size();
	if (count == 1)
	{
		glm::vec2 p = Position[0];
		float w = Width[0];
		glm::vec2 paddedPos = { p.x + 0.01f * w, p.y };

		Position.push_back(paddedPos);
		Width.push_back(w);
		UpdatePositionBuffer();
		UpdateWidthBuffer();

		Position.pop_back();
		Width.pop_back();

		count = 2;
	}
	glBindVertexArray(VertexArray);
	glDrawArrays(GL_LINE_STRIP, 0, count);
}
