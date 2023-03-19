#pragma once

#include "CubicBezier.h"
#include "Stroke.h"

// similar to skleton systems in 3D and 2D 
class CubicBezierBone
{
public:
	Geom::CubicBezier PrevCurve;
	Geom::CubicBezier Curve; // suppose to be PolyCubicBezier, start with simple implementation
	std::vector<float> TBound; // store t values on bezier curve
	Stroke* BoundCurve = nullptr;

	void UpdateOverlay();
	void Update();
	void Reset();
	void Bind(Stroke* s);
};

