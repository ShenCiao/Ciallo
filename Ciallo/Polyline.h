#pragma once
#include <fmt/format.h>

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

		glm::mat2 BoundingBox() const;
		[[nodiscard]] Polyline Translate(glm::vec2 delta) const;
		[[nodiscard]] Polyline Scale(glm::vec2 factor, glm::vec2 pivot = {0.f, 0.f}) const;

		glm::vec2* data();
		void push_back(glm::vec2 point);
		void push_back(float x, float y);
		glm::vec2 pop_back();
		size_t size() const;
		auto begin() { return Points.begin(); }
		auto end() { return Points.end(); }
		void resize(size_t size);
	};
}

template<>
struct fmt::formatter<glm::vec2> : fmt::formatter<std::string>
{
	auto format(glm::vec2 point, fmt::format_context& ctx) -> decltype(ctx.out())
	{
		return fmt::format_to(ctx.out(), "({}, {})", point.x, point.y);
	}
};

template<>
struct fmt::formatter<Geom::Polyline> : fmt::formatter<std::string>
{
	auto format(const Geom::Polyline& line, fmt::format_context& ctx) -> decltype(ctx.out())
	{
		return fmt::format_to(ctx.out(), "End point: {}", line.Points.back());
	}
};


