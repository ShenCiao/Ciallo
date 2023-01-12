#include "pch.hpp"
#include "ColorFace.h"

ColorFace::ColorFace(std::vector<Geom::Polyline> polygonWithHoles): PolygonWithHoles(std::move(polygonWithHoles))
{
}

ColorFace::ColorFace(ColorFace&& other) noexcept
{
	*this = std::move(other);
}

ColorFace& ColorFace::operator=(ColorFace&& other) noexcept
{
	if (this == &other)
		return *this;
	PolygonWithHoles = std::move(other.PolygonWithHoles);
	VertexBuffers = std::move(other.VertexBuffers);
	VertexArray = other.VertexArray;

	other.VertexBuffers.clear();
	other.VertexArray = 0;
	return *this;
}

ColorFace::~ColorFace()
{
	DeleteBuffers();
}

void ColorFace::FillDrawCall()
{
	glClear(GL_STENCIL_BUFFER_BIT);
	glStencilMask(1);
	glColorMask(GL_FALSE, GL_FALSE, GL_FALSE, GL_FALSE);
	glStencilFunc(GL_ALWAYS, 0, 1);
	glStencilOp(GL_KEEP, GL_KEEP, GL_INVERT);
	glBindVertexArray(VertexArray);
	for(int i = 0; i < VertexBuffers.size(); ++i)
	{
		glVertexArrayVertexBuffer(VertexArray, 0, VertexBuffers[i], 0, sizeof(glm::vec2));
		glDrawArrays(GL_TRIANGLE_FAN, 0, PolygonWithHoles[i].size());
	}
	glColorMask(GL_TRUE, GL_TRUE, GL_TRUE, GL_TRUE);
	glStencilFunc(GL_EQUAL, 1, 1);
	glVertexArrayVertexBuffer(VertexArray, 0, VertexBuffers[0], 0, sizeof(glm::vec2));
	glDrawArrays(GL_TRIANGLE_FAN, 0, PolygonWithHoles[0].size());
}

void ColorFace::LineDrawCall()
{
	int count = PolygonWithHoles[0].size();
	glBindVertexArray(VertexArray);
	glDrawArrays(GL_LINE_LOOP, 0, count);
}

void ColorFace::GenUploadBuffers()
{
	DeleteBuffers();

	VertexBuffers.resize(PolygonWithHoles.size());

	glCreateVertexArrays(1, &VertexArray);
	glCreateBuffers(VertexBuffers.size(), VertexBuffers.data());

	glEnableVertexArrayAttrib(VertexArray, 0);
	glVertexArrayAttribBinding(VertexArray, 0, 0);
	glVertexArrayAttribFormat(VertexArray, 0, 2, GL_FLOAT, GL_FALSE, 0);

	for (int i = 0; i < PolygonWithHoles.size(); i++)
	{
		Geom::Polyline& polygon = PolygonWithHoles[i];
		GLuint vbo = VertexBuffers[i];
		glNamedBufferData(vbo, polygon.size() * sizeof(glm::vec2), polygon.data(), GL_DYNAMIC_DRAW);
	}
}

void ColorFace::DeleteBuffers()
{
	glDeleteBuffers(VertexBuffers.size(), VertexBuffers.data());
	VertexBuffers.resize(0);
	glDeleteVertexArrays(1, &VertexArray);
	VertexArray = 0;
}
