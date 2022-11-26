#pragma once

#include "Stroke.h"

class ArrangementManager
{
	//TODO: make insertion and query asynchronous.
public:
	Geom::Arrangement Arrangement{};
	Geom::PointLocation PointLocation{Arrangement};
	std::unordered_map<Stroke*, Geom::Curve_handle> CurveHandleContainer{};
	std::unordered_map<Stroke*, std::vector<Geom::X_monotone_Curve>> CachedQueryCurves{};
	std::unordered_map<Stroke*, std::vector<std::vector<glm::vec2>>> QueryResultsContainer{};
	

	std::map<Stroke*, Geom::Curve> UpdateQueue{};

	void Run();

	void AddOrUpdate(Stroke* stroke);
	void Remove(Stroke* stroke);

	void AddOrUpdateQuery(Stroke* stroke);
	void RemoveQuery(Stroke* stroke);

	std::vector<std::vector<glm::vec2>> PointQuery(glm::vec2 p) const;
	std::vector<std::vector<glm::vec2>> ZoneQuery(const Geom::X_monotone_Curve& monoCurve);
	std::vector<Geom::Face_const_handle> ZoneQueryFace(const Geom::X_monotone_Curve& monoCurve);

	static std::vector<std::vector<glm::vec2>> GetConvexPolygonsFromQueryResult(Geom::PointLocation::Result_type queryResult);
	static std::vector<Geom::Point> VecToPoints(const std::vector<glm::vec2>& vec);
	static std::vector<Geom::Polygon> FaceToPolygon(Geom::Face_const_handle face);
	static std::vector<glm::vec2> PolygonToVec(const Geom::Polygon& polygon);
	static std::vector<Geom::X_monotone_Curve> ConstructXMonotoneCurve(const std::vector<glm::vec2>& polyline);
	static std::vector<glm::vec2> RemoveConsecutiveOverlappingPoint(std::vector<glm::vec2> polyline);
	inline static const Geom::Geom_traits::Construct_curve_2 CurveConstructor =
		Geom::Geom_traits{}.construct_curve_2_object();
	inline static const Geom::Geom_traits::Construct_x_monotone_curve_2 XMonoConstructor =
		Geom::Geom_traits{}.construct_x_monotone_curve_2_object();
};
