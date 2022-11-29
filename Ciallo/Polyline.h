#pragma once

namespace Geom
{
	class Polyline
	{
	public:
		std::vector<glm::vec2> Points;

		Polyline() = default;
		Polyline(const std::vector<glm::vec2>& points);
		glm::vec2* data();
		glm::vec2& operator[](size_t i);
		operator std::vector<glm::vec2>() { return Points; }
		void PushBack(glm::vec2 point);
		void PushBack(float x, float y);
		size_t size() const;
	};
}


