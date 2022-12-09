#pragma once

#include "Polyline.h"

// Fake bezier curve built with polyline.
class CubicBezier
{
	int SegmentCount = 64; 
	std::array<glm::vec2, 4> ControlPoints;
	Geom::Polyline LookUpTable;
public:
	CubicBezier(const std::array<glm::vec2, 4>& points, int segmentCount = 64);
	void EvalLUT();
	glm::vec2 operator()(float t);
	std::array<CubicBezier, 2> Split(float t) const;
	float DerivativeT(float t, int axis = 0);
	float FindT(float given, int axis = 0);
	glm::vec2 FindNearestPoint(glm::vec2 p);

	friend std::ostream& operator<<(std::ostream& os, const CubicBezier& curve);
};
