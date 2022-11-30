#pragma once

namespace Geom
{
	class Polyline
	{
	public:
		std::vector<glm::vec2> Points;

		Polyline() = default;
		Polyline(const std::vector<glm::vec2>& points);
		Polyline(const std::initializer_list<glm::vec2>&);

		glm::vec2& operator[](size_t i);
		operator std::vector<glm::vec2>() { return Points; }

		glm::vec2* data();
		void push_back(glm::vec2 point);
		void push_back(float x, float y);
		glm::vec2 pop_back();
		size_t size() const;
		auto begin() { return Points.begin(); }
		auto end() { return Points.end(); }
	};
}


