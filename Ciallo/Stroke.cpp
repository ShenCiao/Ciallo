#include "pch.hpp"
#include "Stroke.h"

Stroke::Stroke()
{
	GenBuffers();
}

Stroke::~Stroke()
{
	glDeleteBuffers(VertexBuffers.size(), VertexBuffers.data());
	glDeleteBuffers(1, &VertexArray);
}

void Stroke::GenBuffers()
{
	glCreateVertexArrays(1, &VertexArray);
	glCreateBuffers(2, VertexBuffers.data());

	glEnableVertexArrayAttrib(VertexArray, 0);
	glVertexArrayAttribBinding(VertexArray, 0, 0);
	glVertexArrayAttribFormat(VertexArray, 0, 2, GL_FLOAT, GL_FALSE, 0);
	glEnableVertexArrayAttrib(VertexArray, 1);
	glVertexArrayAttribBinding(VertexArray, 1, 1);
	glVertexArrayAttribFormat(VertexArray, 1, 1, GL_FLOAT, GL_FALSE, 0);

	GLintptr offsets[] = { 0, 0 };
	int strides[] = { sizeof(Geom::Point), sizeof(float) };

	glVertexArrayVertexBuffers(VertexArray, 0, VertexBuffers.size(), VertexBuffers.data(), offsets, strides);
}

void Stroke::UploadPositionData()
{
	glNamedBufferData(VertexBuffers[0], Position.size() * sizeof(Geom::Point), Position.data(), GL_DYNAMIC_DRAW);
}

void Stroke::UploadWidthData()
{
	glNamedBufferData(VertexBuffers[1], Width.size() * sizeof(float), Width.data(), GL_DYNAMIC_DRAW);
}
