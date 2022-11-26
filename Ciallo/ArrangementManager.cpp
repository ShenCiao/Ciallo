#include "pch.hpp"
#include "ArrangementManager.h"

#include <algorithm>

void ArrangementManager::Run()
{
	// Insertion and update strokes
	for (auto& [stroke, curve] : UpdateQueue)
	{
		if (CurveHandleContainer.contains(stroke))
		{
			// Warning: this function has been modified to fit our usage!
			CGAL::remove_curve(Arrangement, CurveHandleContainer[stroke]);
			if (curve.number_of_subcurves() == 0) // indicate remove
			{
				CurveHandleContainer.erase(stroke);
				continue;
			}
		}
		
		CurveHandleContainer[stroke] = CGAL::insert(Arrangement, curve);
	}
	UpdateQueue.clear();

	QueryResultsContainer.clear();
	// Run query
	for(auto& [stroke, monoCurves] : CachedQueryCurves)
	{
		std::vector<std::vector<glm::vec2>> vecPolygon;
		std::vector<Geom::Face_const_handle> allFaces;
		for(auto& c : monoCurves)
		{
			auto resultFaces = ZoneQueryFace(c);
			allFaces.insert(allFaces.end(), resultFaces.begin(), resultFaces.end());
		}
		std::set uniqueFaces(allFaces.begin(), allFaces.end());
		for(auto face : uniqueFaces)
		{
			Geom::Polygon outer = FaceToPolygon(face)[0];
			std::list<Geom::Polygon> partitionResult;
			CGAL::approx_convex_partition_2(outer.vertices_begin(), outer.vertices_end(),
				std::back_inserter(partitionResult));
			for(auto& p : partitionResult)
			{
				vecPolygon.push_back(PolygonToVec(p));
			}
		}
		QueryResultsContainer[stroke] = vecPolygon;
	}
}

void ArrangementManager::AddOrUpdate(Stroke* stroke)
{
	auto pos = RemoveConsecutiveOverlappingPoint(stroke->Position);
	if (pos.size() <= 1)
		return;
	UpdateQueue[stroke] = CurveConstructor(VecToPoints(pos));
}

void ArrangementManager::Remove(Stroke* stroke)
{
	UpdateQueue[stroke] = Geom::Curve{};
}

void ArrangementManager::AddOrUpdateQuery(Stroke* stroke)
{
	auto pos = RemoveConsecutiveOverlappingPoint(stroke->Position);
	if (pos.size() <= 1)
		return;
	
	CachedQueryCurves[stroke] = ConstructXMonotoneCurve(pos);
}

void ArrangementManager::RemoveQuery(Stroke* stroke)
{
	CachedQueryCurves.erase(stroke);
}

// return a vector of convex polygons
std::vector<std::vector<glm::vec2>> ArrangementManager::PointQuery(glm::vec2 p) const
{
	// TODO: deal with hole
	Geom::PointLocation::Result_type queryResult = PointLocation.locate({p.x, p.y});
	return GetConvexPolygonsFromQueryResult(queryResult);
}

// For test usage only
std::vector<std::vector<glm::vec2>> ArrangementManager::ZoneQuery(const Geom::X_monotone_Curve& monoCurve)
{
	std::vector<Geom::PointLocation::Result_type> output(256);
	auto beginIt = output.begin();
	auto endIt = CGAL::zone(Arrangement, monoCurve, output.begin(), PointLocation);

	std::vector<std::vector<glm::vec2>> result;
	for(auto it = beginIt; it < endIt; ++it)
	{
		auto polygons = GetConvexPolygonsFromQueryResult(*it);
		result.insert(result.end(), polygons.begin(), polygons.end());
	}
	return result;
}

std::vector<Geom::Face_const_handle> ArrangementManager::ZoneQueryFace(const Geom::X_monotone_Curve& monoCurve)
{
	std::vector<Geom::Face_const_handle> result;
	std::vector<Geom::PointLocation::Result_type> output(256);
	auto beginIt = output.begin();
	auto endIt = CGAL::zone(Arrangement, monoCurve, beginIt, PointLocation);
	for(auto it = beginIt; it < endIt; ++it)
	{
		if (auto faceHandlePtr = boost::get<Geom::Face_const_handle>(&*it))
		{
			if(!(*faceHandlePtr)->is_unbounded())
				result.push_back(*faceHandlePtr);
		}
	}

	return result;
}

std::vector<std::vector<glm::vec2>> ArrangementManager::GetConvexPolygonsFromQueryResult(
	Geom::PointLocation::Result_type queryResult)
{
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

			std::list<Geom::Polygon> partitionResult;
			CGAL::approx_convex_partition_2(outer.vertices_begin(), outer.vertices_end(),
				std::back_inserter(partitionResult));

			std::vector<std::vector<glm::vec2>> result;
			for (auto& poly : partitionResult)
			{
				result.push_back(PolygonToVec(poly));
			}
			return result;
		}
	}
	else
	{
		return {};
	}
}

std::vector<Geom::Point> ArrangementManager::VecToPoints(const std::vector<glm::vec2>& vec)
{
	std::vector<Geom::Point> points;
	for (auto& p : vec)
	{
		points.emplace_back(p.x, p.y);
	}
	return points;
}

// return: index 0 is the boundary, others are holes
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

std::vector<glm::vec2> ArrangementManager::PolygonToVec(const Geom::Polygon& polygon)
{
	std::vector<glm::vec2> result;
	for (auto it = polygon.begin(); it != polygon.end(); ++it)
	{
		result.push_back({CGAL::to_double(it->x()), CGAL::to_double(it->y())});
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

	std::vector<size_t> indices;
	auto cachedDirection = Overlap;
	for (size_t i = 0; i < polyline.size(); ++i)
	{
		if (i + 1 == polyline.size())
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
	for (int i = 0; i < polyline.size(); ++i)
	{
		points[i] = {polyline[i].x, polyline[i].y};
	}

	std::vector<Geom::X_monotone_Curve> result;
	for (int i = 0; i < indices.size() - 1; ++i)
	{
		result.push_back(XMonoConstructor(points.begin() + indices[i], points.begin() + indices[i + 1] + 1));
	}
	return result;
}

std::vector<glm::vec2> ArrangementManager::RemoveConsecutiveOverlappingPoint(std::vector<glm::vec2> polyline)
{
	auto endIt = std::unique(polyline.begin(), polyline.end());
	polyline.resize(std::distance(polyline.begin(), endIt));
	return polyline;
}
