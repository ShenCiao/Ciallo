#include "pch.hpp"
#include "ArrangementManager.h"

void ArrangementManager::Run()
{
	// Insertion and update
	for(Stroke* stroke : InsertQueue)
	{
		auto pos = RemoveConsecutiveOverlappingPoint(stroke->Position);
		if (pos.size() <= 1)
			return;
		if (stroke->CurveHandle != nullptr)
		{
			CGAL::remove_curve(Arrangement, stroke->CurveHandle);
		}

		std::vector<Geom::Geom_traits::Point_2> points;
		for (int i = 0; i < pos.size(); ++i)
		{
			points.emplace_back(pos[i].x, pos[i].y);
		}
		stroke->CurveHandle = CGAL::insert(Arrangement, CurveConstructor(points));
	}
	InsertQueue.clear();
}

void ArrangementManager::InsertOrUpdate(Stroke* stroke)
{
	InsertQueue.push_back(stroke);
}

void ArrangementManager::Remove(Stroke* stroke)
{
	if (stroke->CurveHandle != nullptr)
	{
		CGAL::remove_curve(Arrangement, stroke->CurveHandle);
	}
}

// return a vector of convex polygons
std::vector<std::vector<glm::vec2>> ArrangementManager::PointQuery(glm::vec2 p)
{
	// TODO: deal with hole
	
	auto queryResult = PointLocation.locate({p.x, p.y});

	if (auto faceHandlePtr = boost::get<Geom::Face_const_handle>(&queryResult))
	{
		Geom::Face_const_handle face = *faceHandlePtr;
		if (face->is_unbounded())
		{
			return {};
		}
		else
		{
			// TODO: deal with holes
			std::vector<Geom::Polygon> simplePolygonWithHole = FaceToPolygon(face);
			auto& outer = simplePolygonWithHole[0];

			if (!outer.is_simple())
			{
				spdlog::info("not simple");
			}
			else
			{
				spdlog::info("simple");
			}

			if (outer.is_clockwise_oriented())
				spdlog::info("Clockwise");
			if (outer.is_counterclockwise_oriented())
				spdlog::info("Counterclockwise");

			std::list<Geom::Polygon> partitionResult;
			CGAL::approx_convex_partition_2(outer.vertices_begin(), outer.vertices_end(),
			                                std::back_inserter(partitionResult));

			std::vector<std::vector<glm::vec2>> result;
			for (auto& poly : partitionResult)
			{
				result.push_back(DeconstructPolygon(poly));
			}
			return result;
		}
	}
	else
	{
		return {};
	}
}

std::vector<std::vector<glm::vec2>> ArrangementManager::PolylineQuery(const std::vector<glm::vec2>& polyline)
{
	if (polyline.size() == 0)
		return {};
	if (polyline.size() == 1)
		return PointQuery(polyline[0]);
}

std::vector<Geom::Polygon> ArrangementManager::FaceToPolygon(Geom::Face_const_handle face)
{
	std::vector<Geom::Polygon> result(1);
	auto start = face->outer_ccb();
	auto curr = start;
	bool palindromic = false;
	std::vector<Geom::Halfedge_const_handle> handles{};
	do
	{
		if (!palindromic)
		{
			handles.push_back(curr);
		}

		if (curr->prev() == curr->twin())
		{
			handles.pop_back();
			palindromic = true;
		}

		if (palindromic)
		{
			if (curr->twin() == handles.back())
			{
				handles.pop_back();
			}
			else
			{
				palindromic = false;
				handles.push_back(curr);
			}
		}
	}
	while (++curr != start);

	for (auto halfEdge : handles)
	{
		auto beginIt = halfEdge->curve().points_begin();

		if (halfEdge->source()->point() == *beginIt)
		{
			for (auto it = beginIt; it != --halfEdge->curve().points_end(); ++it)
			{
				result[0].push_back(*it);
			}
		}
		else if (halfEdge->source()->point() == *--halfEdge->curve().points_end())
		{
			for (auto it = --halfEdge->curve().points_end(); it != beginIt; --it)
			{
				result[0].push_back(*it);
			}
		}
		else
		{
			throw std::runtime_error("Something wrong");
		}
	}

	// TODO: push holes
	return result;
}

std::vector<glm::vec2> ArrangementManager::DeconstructPolygon(const Geom::Polygon& polygon)
{
	std::vector<glm::vec2> result;
	for (auto it = polygon.begin(); it != polygon.end(); ++it)
	{
		result.emplace_back(it->x(), it->y());
	}
	return result;
}

std::vector<Geom::X_monotone_Curve> ArrangementManager::ConstructXMonotoneCurve(
	const std::vector<glm::vec2>& polyline)
{
	enum
	{
		LeftToRight = -1,
		RightToLeft = 1,
		Overlap = 0,
	};


	auto calDirection = [](glm::vec2 p0, glm::vec2 p1)
	{
		if (p0.x < p1.x)
		{
			return LeftToRight;
		}
		else if (p0.x > p1.x)
		{
			return RightToLeft;
		}
		else
		{
			if (p0.y < p1.y)
			{
				return LeftToRight;
			}
			else if (p0.y > p1.y)
			{
				return RightToLeft;
			}
			else
			{
				return Overlap;
			}
		}
	};

	std::vector<int> indices;
	auto cachedDirection = Overlap;
	for (int i = 0; i < polyline.size(); ++i)
	{
		if(i+1 == polyline.size())
		{
			indices.push_back(i);
			continue;
		}
		auto currDirection = calDirection(polyline[i], polyline[i + 1]);
		if (currDirection != cachedDirection)
		{
			indices.push_back(i);
			cachedDirection = currDirection;
		}
	}

	std::vector<Geom::Point> points(polyline.size());
	for(int i = 0; i < polyline.size(); ++i)
	{
		points[i] = { polyline[i].x, polyline[i].y };
	}

	std::vector<Geom::X_monotone_Curve> result;
	for(int i = 0; i < indices.size() - 1; ++i)
	{
		result.push_back(XMonoConstructor(points.begin() + indices[i], points.begin() + indices[i + 1] + 1));
	}
	return result;
}

std::vector<glm::vec2> ArrangementManager::RemoveConsecutiveOverlappingPoint(const std::vector<glm::vec2>& polyline)
{
	std::vector<glm::vec2> result;
	for (int i = 0; i < polyline.size(); ++i)
	{
		if (i > 0 && polyline[i] == polyline[i - 1])
		{
			continue;
		}

		result.emplace_back(polyline[i].x, polyline[i].y);
	}
	return result;
}
