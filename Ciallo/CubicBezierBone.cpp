#include "pch.hpp"
#include "CubicBezierBone.h"

#include "Stroke.h"
#include "StrokeContainer.h"
#include "Painter.h"
#include "ArrangementManager.h"

#include <dlib/optimization.h>
#include "TimelineManager.h"

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

void CubicBezierBone::UpdateBoundStroke()
{
	if (BoundStrokeE != entt::null)
	{
		auto& stroke = R.get<Stroke>(BoundStrokeE);
		for (int i = 0; i < TBound.size(); ++i)
		{
			float t = TBound[i];
			glm::vec2 prevPos = PrevCurve(t);
			glm::vec2 pos = Curve(t);
			glm::vec2 delta = pos - prevPos;
			stroke.Position[i] += delta;
		}

		stroke.UpdateBuffers();
		auto& usage = R.get<StrokeUsageFlags>(BoundStrokeE);
		entt::entity drawingE = R.ctx().get<TimelineManager>().GetCurrentDrawing();
		if (drawingE == entt::null) return;
		auto& arm = R.get<ArrangementManager>(drawingE);
		if (!!(usage & StrokeUsageFlags::Arrange)) {
			arm.AddOrUpdate(BoundStrokeE);
		}
		if (!!(usage & StrokeUsageFlags::Zone)) {
			arm.AddOrUpdateQuery(BoundStrokeE);
		}
	}
}

void CubicBezierBone::Update()
{
	Curve.EvalLUT();
	UpdateOverlay();
	UpdateBoundStroke();
	PrevCurve = Curve;
}

void CubicBezierBone::Reset()
{
	PrevCurve = Curve = Geom::CubicBezier{};
	TBound.clear();
	BoundStrokeE = entt::null;

	auto& lines = R.ctx().get<OverlayContainer>().Lines;
	auto& circles = R.ctx().get<OverlayContainer>().Circles;
	lines.clear();
	circles.clear();
}

void CubicBezierBone::Bind(entt::entity e)
{
	BoundStrokeE = e;
	TBound.clear();
	auto& s = R.get<Stroke>(BoundStrokeE);
	for (glm::vec2 p : s.Position)
	{
		float t = Curve.FindNearestPoint(p);
		TBound.push_back(t);
	}
}

void CubicBezierBone::Fit(entt::entity e)
{
	auto& stroke = R.get<Stroke>(e);
	glm::vec2 p0 = stroke.Position[0];
	glm::vec2 p3 = *std::prev(stroke.Position.end());
	float length = stroke.Position.Length();
	float numPoints = stroke.Position.size();
	dlib::matrix<double, 0, 1> params(4);
	params = {p0.x, p0.y, p3.x, p3.y};

	auto rateCurve = [&](const dlib::matrix<double, 0, 1>& params) -> double
	{
		glm::vec2 p1 = {params(0, 0), params(1, 0)};
		glm::vec2 p2 = {params(2, 0), params(3, 0)};
		Geom::CubicBezier curve{{p0, p1, p2, p3}};
		curve.EvalLUT();
		double totDistance = 0.0;
		for (glm::vec2 linePoint : stroke.Position)
		{
			float t = curve.FindNearestPoint(linePoint);
			totDistance += glm::distance(linePoint, curve(t));
		}
		double handleDiff = glm::abs(glm::distance(p0, p1) - glm::distance(p2, p3));
		// double handleLengthPenalty = 0.2 * (glm::distance(p0, p1) + glm::distance(p2, p3)); // cg strategy
		double handleLengthPenalty = 
			glm::distance(p0, p1)/length + 
			glm::distance(p2, p3)/length;

		return totDistance * 50.0/numPoints + 0.1*handleLengthPenalty + 0.5*handleDiff;
	};

	dlib::find_min_using_approximate_derivatives(
		dlib::bfgs_search_strategy(),
		dlib::objective_delta_stop_strategy(),
		rateCurve,
		params,
		-1);
	Curve.ControlPoints[0] = p0;
	Curve.ControlPoints[1] = {params(0, 0), params(1, 0)};
	Curve.ControlPoints[2] = {params(2, 0), params(3, 0)};
	Curve.ControlPoints[3] = p3;
	Update();
}
