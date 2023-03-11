#pragma once

#include "CubicBezier.h"

// similar to skleton systems in 3D and 2D 
class CubicBezierBone
{
public:
	Geom::CubicBezier Curve; // suppose to be PolyCubicBezier, start with simple implementation
	std::vector<float> TBound; // store t values on bezier curve
};

