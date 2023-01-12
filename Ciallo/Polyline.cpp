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

	glm::mat2 Polyline::BoundingBox() const
	{
		
		auto resultX = ranges::minmax(Points|views::transform([](glm::vec2 pos){return pos.x;}));
		auto resultY = ranges::minmax(Points|views::transform([](glm::vec2 pos){return pos.y;}));
		return {{resultX.min, resultY.min}, {resultX.max, resultY.max}};
	}

	Polyline Polyline::Translate(glm::vec2 delta) const
	{
		std::vector<glm::vec2> result;
		result.reserve(Points.size());
		for (auto p : Points)
		{
			result.push_back(p + delta);
		}
		return result;
	}

	Polyline Polyline::Scale(glm::vec2 factor, glm::vec2 pivot) const
	{
		auto curve = *this;

		if(pivot != glm::vec2{0.0f, 0.0f})
		{
			curve = Translate(-pivot);
		}

		for(auto& p : curve)
		{
			p *= factor;
		}
		curve = curve.Translate(pivot);
		return curve;
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

