#pragma once

#include <CGAL/Simple_cartesian.h>
#include <ranges>

#include "Point.h"


namespace Geom
{
	class Polyline
	{
		std::vector<Point> mPoints{};

	public:
		Polyline() = default;

		template <typename Range> requires std::ranges::range<Range>
		explicit Polyline(const Range& points)
			: mPoints(points)
		{
		}

		const Point* data() const
		{
			return mPoints.data();
		}

		int number_of_points() const
		{
			return static_cast<int>(mPoints.size());
		}
	};
}
