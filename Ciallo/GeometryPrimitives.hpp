#pragma once

#include <CGAL/Simple_cartesian.h>
#include "Polyline.h"

namespace Geom
{
	using Kernel = CGAL::Simple_cartesian<float>;
	using Bbox = CGAL::Bbox_2; // Bounding box of double TODO:make a float version
}

