#pragma once

#include "ColorFace.h"

class ArrangementManager
{
	//TODO: make insertion and query asynchronous in a thread.
public:
	CGAL::Arrangement Arrangement{};
	CGAL::PointLocation PointLocation{Arrangement};
	CGAL::Visibility Visibility{}; // attach to arrangement only when needed

	std::unordered_map<entt::entity, CGAL::Curve_handle> CurveHandleContainer{};
	std::unordered_map<entt::entity, std::vector<CGAL::X_monotone_Curve>> CachedQueryCurves{};
	// One stroke may cross multiple polygons with holes.
	std::unordered_map<entt::entity, std::vector<ColorFace>> QueryResultsContainer{};
	

	std::map<entt::entity, CGAL::Curve> UpdateQueue{};

	void Run();

	void AddOrUpdate(entt::entity strokeE);
	void Remove(entt::entity strokeE);

	void AddOrUpdateQuery(entt::entity strokeE);
	void RemoveQuery(entt::entity strokeE);

	Geom::Polyline PointQueryVisibility(glm::vec2 p) const;
	std::vector<Geom::Polyline> PointQuery(glm::vec2 p) const;
	std::vector<std::vector<glm::vec2>> ZoneQuery(const CGAL::X_monotone_Curve& monoCurve);
	std::vector<CGAL::Face_const_handle> ZoneQueryFace(const CGAL::X_monotone_Curve& monoCurve);

	static std::vector<Geom::Polyline> GetConvexPolygonsFromQueryResult(const CGAL::PointLocation::Result_type& queryResult);
	static std::vector<CGAL::Point> VecToPoints(const std::vector<glm::vec2>& vec);
	static std::vector<CGAL::Polygon> FaceToPolygon(CGAL::Face_const_handle face);
	static std::vector<Geom::Polyline> FaceToVec(CGAL::Face_const_handle face);
	static Geom::Polyline PolygonToVec(const CGAL::Polygon& polygon);
	static std::vector<CGAL::X_monotone_Curve> ConstructXMonotoneCurve(const std::vector<glm::vec2>& polyline);
	static std::vector<glm::vec2> RemoveConsecutiveOverlappingPoint(std::vector<glm::vec2> polyline);
	inline static const CGAL::Geom_traits::Construct_curve_2 CurveConstructor =
		CGAL::Geom_traits{}.construct_curve_2_object();
	inline static const CGAL::Geom_traits::Construct_x_monotone_curve_2 XMonoConstructor =
		CGAL::Geom_traits{}.construct_x_monotone_curve_2_object();
};
