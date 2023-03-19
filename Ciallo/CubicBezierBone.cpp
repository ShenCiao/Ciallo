#include "pch.hpp"
#include "CubicBezierBone.h"

#include "StrokeContainer.h"

void CubicBezierBone::UpdateOverlay()
{
	auto& lines = R.ctx().get<OverlayContainer>().Lines;
	auto& circles = R.ctx().get<OverlayContainer>().Circles;
	lines.clear();
	circles.clear();

	lines.push_back(Curve.LookUpTable);
	lines.emplace_back(std::vector{Curve.ControlPoints[0], Curve.ControlPoints[1]});
	lines.emplace_back(std::vector{Curve.ControlPoints[2], Curve.ControlPoints[3]});

	circles.push_back(std::vector{
		Curve.ControlPoints[0],
		Curve.ControlPoints[1],
		Curve.ControlPoints[2],
		Curve.ControlPoints[3]
	});
}

void CubicBezierBone::Update()
{
	Curve.EvalLUT();
	UpdateOverlay();
	if (BoundCurve)
	{
		for (int i = 0; i < TBound.size(); ++i)
		{
			float t = TBound[i];
			glm::vec2 prevPos = PrevCurve(t);
			glm::vec2 pos = Curve(t);
			glm::vec2 delta = pos - prevPos;
			BoundCurve->Position[i] += delta;
		}
		BoundCurve->UpdateBuffers();
	}
	PrevCurve = Curve;
}

void CubicBezierBone::Reset()
{
	PrevCurve = Curve = Geom::CubicBezier{};
	TBound.clear();
	BoundCurve = nullptr;

	auto& lines = R.ctx().get<OverlayContainer>().Lines;
	auto& circles = R.ctx().get<OverlayContainer>().Circles;
	lines.clear();
	circles.clear();
}

void CubicBezierBone::Bind(Stroke* s)
{
	BoundCurve = s;
	TBound.clear();
	for (glm::vec2 p : s->Position)
	{
		float t = Curve.FindNearestPoint(p);
		TBound.push_back(t);
	}
}
