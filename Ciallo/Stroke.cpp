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

void Stroke::OnChanged()
{
	UpdatePositionBuffer();
	UpdateWidthBuffer();
	UpdateArrangement();
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

void Stroke::UpdateArrangement()
{
	if(Position.size() <= 1)
	{
		return;
	}
	static const Geom::Geom_traits traits;
	static const Geom::Geom_traits::Construct_curve_2 curveConstruct =
		traits.construct_curve_2_object();
	
	if(Handle != nullptr)
	{
		CGAL::remove_curve(*Arrangement, Handle);
	}

	std::vector<Geom::Geom_traits::Point_2> ps;
	for(int i = 0; i < Position.size(); ++i)
	{
		if(i > 0 && Position[i] == Position[i - 1])
		{
			continue;
		}

		ps.emplace_back(Position[i].x, Position[i].y);
	}
	if(ps.size() <= 1)
	{
		return;
	}
	
	Geom::Curve c = curveConstruct(ps);
	Handle = CGAL::insert(*Arrangement, c);
}
