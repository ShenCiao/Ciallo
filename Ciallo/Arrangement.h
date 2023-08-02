#pragma once

#include <CGAL/Exact_predicates_inexact_constructions_kernel.h>
#include <CGAL/Exact_predicates_exact_constructions_kernel.h>
#include <CGAL/Arr_polyline_traits_2.h>
#include <CGAL/Arr_walk_along_line_point_location.h>
#include <CGAL/Arrangement_with_history_2.h>
#include <CGAL/partition_2.h>
#include "Visiblity.h"


namespace CGAL
{
	using Kernel = CGAL::Exact_predicates_inexact_constructions_kernel;
	using Segment_traits = CGAL::Arr_segment_traits_2<Kernel>;
	using Geom_traits = CGAL::Arr_polyline_traits_2<Segment_traits>;

	using Point = Geom_traits::Point_2;
	using Curve = Geom_traits::Curve_2; // Polyline
	using X_monotone_curve = Geom_traits::X_monotone_curve_2;

	using Arrangement = CGAL::Arrangement_with_history_2<Geom_traits>;
	using PointLocation = CGAL::Arr_walk_along_line_point_location<Arrangement>;
	// This is a customized version of Triangular_expansion_visibility
	using Visibility = CGAL::Triangular_expansion_visibility_2<Arrangement, CGAL::Tag_true>;
	using VisOutputArr = CGAL::Arrangement_2<Segment_traits>; // Arrangement class used for output the vis result.

	using Vertex_const_handle = Arrangement::Vertex_const_handle;
	using Edge_const_handle = Arrangement::Halfedge_const_handle;
	using Face_const_handle = Arrangement::Face_const_handle; 
	using Halfedge_const_handle = Arrangement::Halfedge_const_handle;
	using Curve_handle = Arrangement::Curve_handle;

	using Partition_traits = CGAL::Partition_traits_2<Kernel>;
	using Polygon = Partition_traits::Polygon_2;
}
