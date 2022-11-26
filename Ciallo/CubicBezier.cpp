#include "pch.hpp"
#include "CubicBezier.h"

#include <boost/math/tools/roots.hpp>
#include <glm/gtx/exterior_product.hpp>

CubicBezier::CubicBezier(const std::array<glm::vec2, 4>& points): Points(points)
{
	EvalLUT();
}

glm::vec2 CubicBezier::operator()(float t) const
{
	int i = glm::floor(t * SEG_N);
	if (i == SEG_N)
	{
		return LookUpTable[i];
	}
	else
	{
		return glm::mix(LookUpTable[i], LookUpTable[i + 1], glm::mod(t * SEG_N, 1.0f));
	}
}

std::array<CubicBezier, 2> CubicBezier::Split(float t) const
{
	// assumed origin curve is in left to right order
	std::array<glm::vec2, 4> left{}, right{};

	left[0] = Points[0];
	left[1] = t * Points[1] - (t - 1) * Points[0];
	left[2] = t * t * Points[2] - 2.0f * t * (t - 1) * Points[1] + (t - 1) * (t - 1) * Points[0];
	left[3] =
		glm::pow(t, 3.0f) * Points[3] -
		3.0f * t * t * (t - 1) * Points[2] +
		3.0f * t * (t - 1) * (t - 1) * Points[1] -
		glm::pow(t - 1, 3.0f) * Points[0];


	right[0] =
		glm::pow(t, 3.0f) * Points[3] -
		3.0f * t * t * (t - 1) * Points[2] +
		3.0f * t * (t - 1) * (t - 1) * Points[1] -
		glm::pow(t - 1, 3.0f) * Points[0];
	right[1] = t * t * Points[3] - 2.0f * t * (t - 1) * Points[2] + (t - 1) * (t - 1) * Points[1];
	right[2] = t * Points[3] - (t - 1) * Points[2];
	right[3] = Points[3];

	return {CubicBezier{left}, CubicBezier{right}};
}

float CubicBezier::DerivativeT(float t, int axis) const
{
	int i = glm::floor(t * SEG_N);
	if (i == SEG_N)
	{
		return DerivativeT((SEG_N - 1) * 1.0f / SEG_N, axis);
	}
	else
	{
		return (LookUpTable[i + 1] - LookUpTable[i])[axis] / (1.0f / SEG_N);
	}
}

void CubicBezier::EvalLUT()
{
	// Implement Casteljau's algorithm for better performance when needed.
	for (int i = 0; i <= SEG_N; ++i)
	{
		float t = static_cast<float>(i) / static_cast<float>(SEG_N);
		LookUpTable[i] =
			Points[0] * glm::pow(1 - t, 3.0f) +
			3.0f * Points[1] * (1 - t) * (1 - t) * t +
			3.0f * Points[2] * (1 - t) * t * t +
			Points[3] * glm::pow(t, 3.0f);
	}
}

/**
* \brief Find the t value at given x or y value.
* Only within range 0.0-1.0. Only find one value closer(?) to 0.0. May need change.
* \param given The given value.
* \param axis X or Y axis to query.
* \return T value if t is found, else the min value of float.
*/
float CubicBezier::FindT(float given, int axis) const
{
	using boost::math::tools::newton_raphson_iterate;

	auto target = [given, axis, this](float var)
	{
		return std::make_pair(operator()(var)[axis] - given, DerivativeT(var, axis));
	};

	const int digits = std::numeric_limits<float>::digits;
	boost::uintmax_t maxIt = 32;
	float result = newton_raphson_iterate(target, 0.0f, 0.0f, 1.0f, digits, maxIt);

	if (std::abs(target(result).first) >= 1e-2f)
	{
		return std::numeric_limits<float>::min();
	}
	return result;
}

/**
 * \return t value of nearest point.
 */
float CubicBezier::FindNearestT(glm::vec2 p) const
{
	auto it = std::max_element(LookUpTable.begin(), LookUpTable.end(), [p](glm::vec2 a, glm::vec2 b)
	{
		return glm::distance(a, p) < glm::distance(b, p);
	});
	int i = std::distance(it, LookUpTable.begin());
	if (i == SEG_N)
	{
		float dotProduct = glm::dot(LookUpTable[SEG_N] - LookUpTable[SEG_N - 1], p - LookUpTable[SEG_N]);
		if (dotProduct >= 0) return 1.0f;

		float ratio = 1.0f - glm::abs(dotProduct) / glm::distance(LookUpTable[SEG_N], LookUpTable[SEG_N - 1]);
		return (SEG_N - 1 + ratio) / SEG_N;
	}
	if (i == 0)
	{
		float dotProduct = glm::dot(LookUpTable[1] - LookUpTable[0], p - LookUpTable[0]);
		if (dotProduct <= 0) return 0.0f;

		float ratio = 1 - glm::abs(dotProduct) / glm::distance(LookUpTable[0], LookUpTable[1]);
		return ratio / SEG_N;
	}

	glm::vec2 start = LookUpTable[i];
	glm::vec2 after = LookUpTable[i + 1];
	glm::vec2 before = LookUpTable[i - 1];
	float d_after = glm::abs(glm::cross(p - start, start - after)) / glm::distance(start, after);
	float d_before = glm::abs(glm::cross(p - start, start - before)) / glm::distance(start, before);
}

std::ostream& operator<<(std::ostream& os, const CubicBezier& curve)
{
	for (int i = 0; i < 4; ++i)
	{
		os << "(" << curve.Points[i].x << " " << curve.Points[i].y << ")  ";
	}
	return os;
}
