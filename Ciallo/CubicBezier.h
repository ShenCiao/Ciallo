#pragma once

#include "Polyline.h"

// Fake bezier curve built with polyline.
class CubicBezier
{
public:
	int SegmentCount = 64; 
	glm::mat4x2 ControlPoints;
	Geom::Polyline LookUpTable;

	CubicBezier() = default;
	CubicBezier(glm::mat4x2 points, int segmentCount = 64);
	void EvalLUT();
	glm::vec2 operator()(float t);
	std::array<CubicBezier, 2> Split(float t) const;
	float DerivativeT(float t, int axis = 0);
	float FindT(float given, int axis = 0);
	glm::vec2 FindNearestPoint(glm::vec2 p);

	friend std::ostream& operator<<(std::ostream& os, const CubicBezier& curve);
};
