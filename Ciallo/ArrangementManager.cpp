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

	if(UpdateQueue.size() != 0 && Visibility.is_attached())
	{
		Visibility.init_cdt();
	}

	UpdateQueue.clear();

	QueryResultsContainer.clear();
	// Run query
	for (auto& [stroke, monoCurves] : CachedQueryCurves)
	{
		std::vector<ColorFace> vecPolygons;
		std::vector<CGAL::Face_const_handle> allFaces;
		for (auto& c : monoCurves)
		{
			auto resultFaces = ZoneQueryFace(c);
			allFaces.insert(allFaces.end(), resultFaces.begin(), resultFaces.end());
		}
		std::set uniqueFaces(allFaces.begin(), allFaces.end());
		for (const auto& face : uniqueFaces)
		{
			std::vector<Geom::Polyline> polygonWithHoles = FaceToVec(face);
			vecPolygons.emplace_back(polygonWithHoles);
		}
		QueryResultsContainer[stroke] = std::move(vecPolygons);
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
	UpdateQueue[stroke] = CGAL::Curve{};
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

Geom::Polyline ArrangementManager::PointQueryVisibility(glm::vec2 p) const
{
	CGAL::PointLocation::Result_type queryResult = PointLocation.locate({p.x, p.y});
	if (auto faceHandlePtr = boost::get<CGAL::Face_const_handle>(&queryResult))
	{
		CGAL::Face_const_handle face = *faceHandlePtr;
		if (face->is_unbounded())
			return {};

		CGAL::VisOutputArr arr;
		CGAL::VisOutputArr::Face_const_handle output = Visibility.compute_visibility_in_bounded_face({p.x, p.y}, arr);

		auto start = output->outer_ccb();
		auto curr = start;
		Geom::Polyline result;
		do
		{
			result.push_back(CGAL::to_double(curr->source()->point().x()), CGAL::to_double(curr->source()->point().y()));
		} while (++curr != start);
		return result;
	}
	return {};
}

// return a vector of convex polygons
std::vector<Geom::Polyline> ArrangementManager::PointQuery(glm::vec2 p) const
{
	CGAL::PointLocation::Result_type queryResult = PointLocation.locate({p.x, p.y});
	if (auto faceHandlePtr = boost::get<CGAL::Face_const_handle>(&queryResult))
	{
		CGAL::Face_const_handle face = *faceHandlePtr;
		if (face->is_unbounded())
			return {};
		return FaceToVec(face);
	}
	return {};
}

// For test usage only
std::vector<std::vector<glm::vec2>> ArrangementManager::ZoneQuery(const CGAL::X_monotone_Curve& monoCurve)
{
	std::vector<CGAL::PointLocation::Result_type> output(256);
	auto beginIt = output.begin();
	auto endIt = CGAL::zone(Arrangement, monoCurve, output.begin(), PointLocation);

	std::vector<std::vector<glm::vec2>> result;
	for (auto it = beginIt; it < endIt; ++it)
	{
		auto polygons = GetConvexPolygonsFromQueryResult(*it);
		result.insert(result.end(), polygons.begin(), polygons.end());
	}
	return result;
}

// Only unbounded face returned
std::vector<CGAL::Face_const_handle> ArrangementManager::ZoneQueryFace(const CGAL::X_monotone_Curve& monoCurve)
{
	std::vector<CGAL::Face_const_handle> result;
	std::vector<CGAL::PointLocation::Result_type> output(256);
	auto beginIt = output.begin();
	auto endIt = CGAL::zone(Arrangement, monoCurve, beginIt, PointLocation);
	for (auto it = beginIt; it < endIt; ++it)
	{
		if (auto faceHandlePtr = boost::get<CGAL::Face_const_handle>(&*it))
		{
			if (!(*faceHandlePtr)->is_unbounded())
				result.push_back(*faceHandlePtr);
		}
	}

	return result;
}

std::vector<Geom::Polyline> ArrangementManager::GetConvexPolygonsFromQueryResult(
	const CGAL::PointLocation::Result_type& queryResult)
{
	// TODO: deal with hole
	if (auto faceHandlePtr = boost::get<CGAL::Face_const_handle>(&queryResult))
	{
		CGAL::Face_const_handle face = *faceHandlePtr;
		if (face->is_unbounded())
		{
			return {};
		}
		else
		{
			// TODO: deal with holes
			std::vector<CGAL::Polygon> simplePolygonWithHole = FaceToPolygon(face);
			auto& outer = simplePolygonWithHole[0];

			std::list<CGAL::Polygon> partitionResult;
			CGAL::approx_convex_partition_2(outer.vertices_begin(), outer.vertices_end(),
			                                std::back_inserter(partitionResult));

			std::vector<Geom::Polyline> result;
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

std::vector<CGAL::Point> ArrangementManager::VecToPoints(const std::vector<glm::vec2>& vec)
{
	std::vector<CGAL::Point> points;
	for (auto& p : vec)
	{
		points.emplace_back(p.x, p.y);
	}
	return points;
}


/**
 * \brief Get the polygon from face.
 * If there is a line inserted into a face but not across it, CGAL will return vertices associated with this line.
 * So we need to eliminate it with a palindromic detection.
 * \param face Face handle to get polygon from.
 * \return index 0 is the boundary, others are holes. Hole is not implemented yet.
 */
std::vector<CGAL::Polygon> ArrangementManager::FaceToPolygon(CGAL::Face_const_handle face)
{
	std::vector<CGAL::Polygon> result;

	std::vector<CGAL::Arrangement::Ccb_halfedge_const_circulator> starters;
	starters.push_back(face->outer_ccb());
	for (auto hole = face->holes_begin(); hole != face->holes_end(); ++hole)
	{
		starters.push_back(*hole);
	}


	for (auto start : starters)
	{
		auto curr = start;
		bool palindromic = false;

		std::vector<CGAL::Halfedge_const_handle> handles{};
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

		if (!handles.empty())
			result.emplace_back();
		for (auto halfEdge : handles)
		{
			// it's so annoying that cannot `halfEdge->curve().points_end()-1`
			auto beginIt = halfEdge->curve().points_begin();

			if (halfEdge->source()->point() == *beginIt)
			{
				for (auto it = beginIt; it != --halfEdge->curve().points_end(); ++it)
				{
					result.back().push_back(*it);
				}
			}
			else if (halfEdge->source()->point() == *--halfEdge->curve().points_end())
			{
				for (auto it = --halfEdge->curve().points_end(); it != beginIt; --it)
				{
					result.back().push_back(*it);
				}
			}
			else
			{
				throw std::runtime_error("Something wrong");
			}
		}
	}

	return result;
}

/**
 * \brief Get the polygon vector from face.
 *  Dealing with the line inserted. Member function `FaceToPolygon` is dealing with it too.
 * \param face Face handle to get polygon from.
 * \return index 0 is the boundary, others are holes. Hole is not implemented yet.
 */
std::vector<Geom::Polyline> ArrangementManager::FaceToVec(CGAL::Face_const_handle face)
{
	std::vector<Geom::Polyline> result;
	std::vector<CGAL::Polygon> polygonWithHoles = FaceToPolygon(face);

	for (auto& polygon : polygonWithHoles)
	{
		result.push_back(PolygonToVec(polygon));
	}
	return result;
}

Geom::Polyline ArrangementManager::PolygonToVec(const CGAL::Polygon& polygon)
{
	Geom::Polyline result;
	for (auto it = polygon.begin(); it != polygon.end(); ++it)
	{
		result.push_back({CGAL::to_double(it->x()), CGAL::to_double(it->y())});
	}
	return result;
}

std::vector<CGAL::X_monotone_Curve> ArrangementManager::ConstructXMonotoneCurve(
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

	std::vector<CGAL::Point> points(polyline.size());
	for (int i = 0; i < polyline.size(); ++i)
	{
		points[i] = {polyline[i].x, polyline[i].y};
	}

	std::vector<CGAL::X_monotone_Curve> result;
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
