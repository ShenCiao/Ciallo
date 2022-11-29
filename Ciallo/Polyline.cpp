#include "pch.hpp"
#include "Polyline.h"

namespace Geom
{
	Polyline::Polyline(const std::vector<glm::vec2>& points): Points(points)
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

	void Polyline::PushBack(glm::vec2 point)
	{
		Points.push_back(point);
	}

	void Polyline::PushBack(float x, float y)
	{
		Points.emplace_back(x, y);
	}

	size_t Polyline::size() const
	{
		return Points.size();
	}
}

