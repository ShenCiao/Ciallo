#pragma once

#include <CGAL/Simple_cartesian.h>

namespace Geom
{
	using Kernel = CGAL::Simple_cartesian<float>;
	using Point = Kernel::Point_2;
}
