#include "pch.hpp"
#include "ArrangementSystem.h"

void ArrangementSystem::InsertOrUpdate(Stroke* stroke)
{
	auto& pos = stroke->Position;
	if (pos.size() <= 1)
		return;
	if(stroke->CurveHandle != nullptr)
	{
		CGAL::remove_curve(Arrangement, stroke->CurveHandle);
	}

	std::vector<Geom::Geom_traits::Point_2> points;
	for (int i = 0; i < pos.size(); ++i)
	{
		if (i > 0 && pos[i] == pos[i - 1])
		{
			continue;
		}

		points.emplace_back(pos[i].x, pos[i].y);
	}
	stroke->CurveHandle = CGAL::insert(Arrangement, curveConstruct(points));
}

void ArrangementSystem::Remove(Stroke* stroke)
{
	if (stroke->CurveHandle != nullptr)
	{
		CGAL::remove_curve(Arrangement, stroke->CurveHandle);
	}
}

std::vector<glm::vec2> ArrangementSystem::PointQuery(glm::vec2 p)
{
	// TODO: deal with hole
	auto start = chrono::high_resolution_clock::now();
	auto result = PointLocation.locate({p.x, p.y});
	chrono::duration<double, std::milli> duration = chrono::high_resolution_clock::now() - start;

	if (auto faceHandlePtr = boost::get<Geom::Face_const_handle>(&result))
	{
		Geom::Face_const_handle face = *faceHandlePtr;
		if(face->is_unbounded())
		{
			return {};
		}
		else
		{
			return FaceToPolygon(face);
		}
	}
	else
	{
		return {};
	}
}

std::vector<glm::vec2> ArrangementSystem::FaceToPolygon(Geom::Face_const_handle face)
{
	std::vector<glm::vec2> result;
	auto start = face->outer_ccb();
	auto curr = start;
	do
	{
		for (auto it = curr->curve().points_begin(); it != --curr->curve().points_end(); ++it)
		{
			result.emplace_back(it->x(), it->y());
		}
	}
	while (--curr != start);
	return result;
}
