﻿#include "pch.hpp"
#include "CubicBezier.h"

#include <boost/math/tools/roots.hpp>
#include <glm/gtx/exterior_product.hpp>

namespace Geom
{
	CubicBezier::CubicBezier(glm::mat4x2 points, int segmentCount) :
		SegmentCount(segmentCount),
		ControlPoints(points)
	{
		EvalLUT();
	}

	glm::vec2 CubicBezier::operator()(float t)
	{
		int i = TToIndex(t);
		if (i == SegmentCount)
		{
			return LookUpTable[i];
		}
		else
		{
			return glm::mix(LookUpTable[i], LookUpTable[i + 1], glm::mod(t * SegmentCount, 1.0f));
		}
	}

	std::array<CubicBezier, 2> CubicBezier::Split(float t) const
	{
		// assumed origin curve is in left to right order
		glm::mat4x2 left{}, right{};

		left[0] = ControlPoints[0];
		left[1] = t * ControlPoints[1] - (t - 1) * ControlPoints[0];
		left[2] = t * t * ControlPoints[2] - 2.0f * t * (t - 1) * ControlPoints[1] + (t - 1) * (t - 1) * ControlPoints[
			0];
		left[3] =
			glm::pow(t, 3.0f) * ControlPoints[3] -
			3.0f * t * t * (t - 1) * ControlPoints[2] +
			3.0f * t * (t - 1) * (t - 1) * ControlPoints[1] -
			glm::pow(t - 1, 3.0f) * ControlPoints[0];


		right[0] =
			glm::pow(t, 3.0f) * ControlPoints[3] -
			3.0f * t * t * (t - 1) * ControlPoints[2] +
			3.0f * t * (t - 1) * (t - 1) * ControlPoints[1] -
			glm::pow(t - 1, 3.0f) * ControlPoints[0];
		right[1] = t * t * ControlPoints[3] - 2.0f * t * (t - 1) * ControlPoints[2] + (t - 1) * (t - 1) * ControlPoints[
			1];
		right[2] = t * ControlPoints[3] - (t - 1) * ControlPoints[2];
		right[3] = ControlPoints[3];

		return {CubicBezier{left}, CubicBezier{right}};
	}

	float CubicBezier::DerivativeT(float t, int axis)
	{
		int i = TToIndex(t);
		if (i == SegmentCount)
		{
			return DerivativeT((SegmentCount - 1) * 1.0f / SegmentCount, axis);
		}
		else
		{
			return (LookUpTable[i + 1] - LookUpTable[i])[axis] / (1.0f / SegmentCount);
		}
	}

	void CubicBezier::EvalLUT()
	{
		// Implement Casteljau's algorithm for better performance when needed.
		LookUpTable.resize(SegmentCount + 1);
		for (int i = 0; i <= SegmentCount; ++i)
		{
			float t = IndexToT(i);
			LookUpTable[i] =
				ControlPoints[0] * glm::pow(1 - t, 3.0f) +
				3.0f * ControlPoints[1] * (1 - t) * (1 - t) * t +
				3.0f * ControlPoints[2] * (1 - t) * t * t +
				ControlPoints[3] * glm::pow(t, 3.0f);
		}
	}

	/**
	* \brief Find the t value at given x or y value.
	* Only within range 0.0-1.0. Only find one value closer(?) to 0.0. May need change.
	* \param given The given value.
	* \param axis X or Y axis to query.
	* \return T value if t is found, else the min value of float.
	*/
	float CubicBezier::FindT(float given, int axis)
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
	float CubicBezier::FindNearestPoint(glm::vec2 p)
	{
		auto it = std::min_element(LookUpTable.begin(), LookUpTable.end(),
		                           [p](glm::vec2 a, glm::vec2 b)
		                           {
			                           return glm::distance(a, p) < glm::distance(b, p);
		                           });
		int i = static_cast<int>(std::distance(LookUpTable.begin(), it));

		// left to right assumed
		float lDist, rDist; // distance to segments
		glm::vec2 lNear, rNear; // nearest point on segments
		float lNearT, rNearT; // t value of nearest point

		if (i == 0)
		{
			lDist = std::numeric_limits<float>::max();
			lNear = LookUpTable[0];
			lNearT = 0.0f;
		}
		else
		{
			glm::vec2 tan = glm::normalize(LookUpTable[i] - LookUpTable[i - 1]);
			if (float d = glm::dot(tan, p - LookUpTable[i]); d >= 0)
			{
				lDist = glm::distance(p, LookUpTable[i]);
				lNear = LookUpTable[i];
				lNearT = IndexToT(i);
			}
			else
			{
				lDist = glm::abs(glm::cross(tan, p - LookUpTable[i]));
				lNear = d * tan + LookUpTable[i];
				lNearT = IndexToT(i) + d / glm::distance(LookUpTable[i], LookUpTable[i - 1]) * (1.0f / SegmentCount);
			}
		}

		if (i == SegmentCount)
		{
			rDist = std::numeric_limits<float>::max();
			rNear = LookUpTable[SegmentCount];
			rNearT = 1.0f;
		}
		else
		{
			glm::vec2 tan = glm::normalize(LookUpTable[i + 1] - LookUpTable[i]);
			if (float d = glm::dot(tan, p - LookUpTable[i]); d <= 0)
			{
				rDist = glm::distance(p, LookUpTable[i]);
				rNear = LookUpTable[i];
				rNearT = IndexToT(i);
			}
			else
			{
				rDist = glm::abs(glm::cross(tan, p - LookUpTable[i]));
				rNear = d * tan + LookUpTable[i];
				rNearT = IndexToT(i) + d / glm::distance(LookUpTable[i + 1], LookUpTable[i]) * (1.0f / SegmentCount);
			}
		}
		return lDist < rDist ? lNearT : rNearT;
	}

	float CubicBezier::IndexToT(int index) const
	{
		return static_cast<float>(index) / static_cast<float>(SegmentCount);
	}

	int CubicBezier::TToIndex(float t) const
	{
		return static_cast<int>(glm::floor(t * SegmentCount));
	}

	std::ostream& operator<<(std::ostream& os, const CubicBezier& curve)
	{
		for (int i = 0; i < 4; ++i)
		{
			os << "(" << curve.ControlPoints[i].x << " " << curve.ControlPoints[i].y << ")  ";
		}
		return os;
	}
}
