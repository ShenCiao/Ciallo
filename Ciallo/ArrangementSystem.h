#pragma once

#include "Stroke.h"

class ArrangementSystem
{
	//TODO: make insertion and query asynchronous.
public:
	Geom::Arrangement Arrangement{};
	Geom::PointLocation PointLocation{Arrangement};
	const Geom::Geom_traits::Construct_curve_2 curveConstruct =
		Geom::Geom_traits{}.construct_curve_2_object();

	void InsertOrUpdate(Stroke* stroke);
	void Remove(Stroke* stroke);
	std::vector<glm::vec2> PointQuery(glm::vec2 p);

	static std::vector<glm::vec2> FaceToPolygon(Geom::Face_const_handle face);
};
