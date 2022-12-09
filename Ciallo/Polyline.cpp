#include "pch.hpp"
#include "Polyline.h"

namespace Geom
{
	Polyline::Polyline(const std::vector<glm::vec2>& points): Points(points)
	{
	}

	Polyline::Polyline(const std::initializer_list<glm::vec2>& points) : Points(points)
	{
	}

	glm::vec2* Polyline::data()
	{
		return Points.data();
	}

	glm::vec2& Polyline::operator[](size_t i)
	{
		return Points[i];
	}

	void Polyline::push_back(glm::vec2 point)
	{
		Points.push_back(point);
	}

	void Polyline::push_back(float x, float y)
	{
		Points.emplace_back(x, y);
	}

	glm::vec2 Polyline::pop_back()
	{
		glm::vec2 p = Points.back();
		Points.pop_back();
		return p;
	}

	size_t Polyline::size() const
	{
		return Points.size();
	}

	void Polyline::resize(size_t size)
	{
		Points.resize(size);
	}
}

