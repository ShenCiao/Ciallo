#pragma once

#include "CubicBezier.h"


// similar to skleton systems in 3D and 2D 
class CubicBezierBone
{
public:
	Geom::CubicBezier PrevCurve;
	Geom::CubicBezier Curve; // suppose to be PolyCubicBezier, start with simple implementation
	std::vector<float> TBound; // store t values on bezier curve
	entt::entity BoundStrokeE = entt::null;

	void UpdateOverlay();
	void Update();
	void Reset();
	void Bind(entt::entity e);
	void Fit(entt::entity e);
};

