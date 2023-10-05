#include "pch.hpp"
#include "ArrangementManager.h"

#include <algorithm>
#include "Stroke.h"

void ArrangementManager::Run()
{
	// Insertion and update strokes
	auto start = chrono::high_resolution_clock::now();
	for (auto& [strokeE, curve] : UpdateQueue)
	{
		if (CurveHandleContainer.contains(strokeE))
		{
			// Warning: `remove_curve` has been modified to fit our usage!
			CGAL::remove_curve(Arrangement, CurveHandleContainer[strokeE]);
			if (curve.number_of_subcurves() == 0) // indicate remove
			{
				CurveHandleContainer.erase(strokeE);
				continue;
			}
		}

		CurveHandleContainer[strokeE] = CGAL::insert(Arrangement, curve);
	}
	chrono::duration<double, std::milli> duration = chrono::high_resolution_clock::now() - start;
	if (LogSpeed)
	{
		spdlog::info("Arrangement Time: {}ms", duration.count());
		LogSpeed = false;
	}

	UpdateQueue.clear();

	QueryResultsContainer.clear();
	// Run query
	for (auto& [e, monoCurves] : CachedQueryCurves)
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
		QueryResultsContainer[e] = std::move(vecPolygons);
	}
}

void ArrangementManager::AddOrUpdate(entt::entity strokeE)
{
	auto& stroke = R.get<Stroke>(strokeE);
	auto pos = RemoveConsecutiveOverlappingPoint(stroke.Position);
	if (pos.size() <= 1)
		return;
	UpdateQueue[strokeE] = CurveConstructor(VecToPoints(pos));
}

void ArrangementManager::Remove(entt::entity strokeE)
{
	UpdateQueue[strokeE] = CGAL::Curve{};
}

void ArrangementManager::AddOrUpdateQuery(entt::entity strokeE)
{
	auto& stroke = R.get<Stroke>(strokeE);
	auto pos = RemoveConsecutiveOverlappingPoint(stroke.Position);
	if (pos.size() <= 1)
		return;

	CachedQueryCurves[strokeE] = ConstructXMonotoneCurve(pos);
}

void ArrangementManager::RemoveQuery(entt::entity strokeE)
{
	CachedQueryCurves.erase(strokeE);
}

Geom::Polyline ArrangementManager::PointQueryVisibility(glm::vec2 p) const
{
	CGAL::PointLocation::Result_type queryResult = PointLocation.locate({p.x, p.y});
	if (auto faceHandlePtr = boost::get<CGAL::Face_const_handle>(&queryResult))
	{
		CGAL::Face_const_handle face = *faceHandlePtr;

		CGAL::VisOutputArr arr;
		CGAL::VisOutputArr::Face_const_handle output = Visibility.compute_visibility_in_bounded_face({p.x, p.y}, arr);

		auto start = output->outer_ccb();
		auto curr = start;
		Geom::Polyline result;
		do
		{
			result.push_back(CGAL::to_double(curr->source()->point().x()),
			                 CGAL::to_double(curr->source()->point().y()));
		}
		while (++curr != start);
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

// Only unbounded face returned
std::vector<CGAL::Face_const_handle> ArrangementManager::ZoneQueryFace(const CGAL::X_monotone_curve& monoCurve)
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
 * \brief Get the polygon vector from face.
 * If there is a line inserted into a face but not across it, CGAL will return vertices associated with this line.
 * So we need to eliminate it with a palindromic detection.
 * \param face Face handle to get polygon from.
 * \return index 0 is the boundary, others are holes.
 */
std::vector<Geom::Polyline> ArrangementManager::FaceToVec(CGAL::Face_const_handle face)
{
	std::vector<Geom::Polyline> result;
	// std::vector<CGAL::Polygon> polygonWithHoles = FaceToPolygon(face);

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
				if (!handles.empty() && curr->twin() == handles.back())
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
					result.back().push_back({CGAL::to_double(it->x()), CGAL::to_double(it->y())});
				}
			}
			else if (halfEdge->source()->point() == *--halfEdge->curve().points_end())
			{
				for (auto it = --halfEdge->curve().points_end(); it != beginIt; --it)
				{
					result.back().push_back({CGAL::to_double(it->x()), CGAL::to_double(it->y())});
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

std::vector<CGAL::X_monotone_curve> ArrangementManager::ConstructXMonotoneCurve(
	const std::vector<glm::vec2>& polyline)
{

	std::vector<CGAL::Point> points(polyline.size());
	for (int i = 0; i < polyline.size(); ++i)
	{
		points[i] = {polyline[i].x, polyline[i].y};
	}

	CGAL::Curve curve = CurveConstructor(points);
	using Make_x_monotone_result = boost::variant<CGAL::Point, CGAL::X_monotone_curve>;
	std::list<Make_x_monotone_result> x_objects;
	XMonoMaker(curve, std::back_inserter(x_objects));

	std::vector<CGAL::X_monotone_curve> result;
	for (const auto& x_obj : x_objects) 
	{
		const auto* x_curve = boost::get<CGAL::X_monotone_curve>(&x_obj);
		if (x_curve != nullptr) 
		{
			result.push_back(*x_curve);
		}
	}
	return result;
}

std::vector<glm::vec2> ArrangementManager::RemoveConsecutiveOverlappingPoint(std::vector<glm::vec2> polyline)
{
	auto endIt = std::unique(polyline.begin(), polyline.end());
	polyline.resize(std::distance(polyline.begin(), endIt));
	return polyline;
}
