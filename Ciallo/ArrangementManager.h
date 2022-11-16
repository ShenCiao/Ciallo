#pragma once

#include "Stroke.h"

class ArrangementManager
{
	//TODO: make insertion and query asynchronous.
public:
	Geom::Arrangement Arrangement{};
	Geom::PointLocation PointLocation{Arrangement};

	std::vector<Stroke*> InsertQueue;

	void Run();

	void InsertOrUpdate(Stroke* stroke);
	void Remove(Stroke* stroke);
	std::vector<std::vector<glm::vec2>> PointQuery(glm::vec2 p);
	std::vector<std::vector<glm::vec2>> PolylineQuery(const std::vector<glm::vec2>& polyline);

	// index 0 is the boundary, others are holes
	static std::vector<Geom::Polygon> FaceToPolygon(Geom::Face_const_handle face);
	static std::vector<glm::vec2> DeconstructPolygon(const Geom::Polygon& polygon);
	static std::vector<Geom::X_monotone_Curve> ConstructXMonotoneCurve(const std::vector<glm::vec2>& polyline);
	static std::vector<glm::vec2> RemoveConsecutiveOverlappingPoint(const std::vector<glm::vec2>& polyline);
	inline static const Geom::Geom_traits::Construct_curve_2 CurveConstructor =
		Geom::Geom_traits{}.construct_curve_2_object();
	inline static const Geom::Geom_traits::Construct_x_monotone_curve_2 XMonoConstructor =
		Geom::Geom_traits{}.construct_x_monotone_curve_2_object();
};
