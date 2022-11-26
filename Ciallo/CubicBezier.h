#pragma once

class CubicBezier
{
	static constexpr int SEG_N = 256; // number of segments.
	std::array<glm::vec2, 4> Points;
	std::array<glm::vec2, SEG_N + 1> LookUpTable; // Need a polyline class here
public:
	CubicBezier(const std::array<glm::vec2, 4>& points);
	void EvalLUT();
	glm::vec2 operator()(float t) const;
	std::array<CubicBezier, 2> Split(float t) const;
	float DerivativeT(float t, int axis = 0) const;
	float FindT(float given, int axis = 0) const;
	float FindNearestT(glm::vec2 p) const;

	friend std::ostream& operator<<(std::ostream& os, const CubicBezier& curve);
};
