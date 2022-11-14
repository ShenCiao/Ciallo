#pragma once

#include "ArticulatedLineEngine.h"
#include "PolygonEngine.h"
#include "Drawing.h"

class RenderingSystem
{
public:
	static inline std::unique_ptr<ArticulatedLineEngine> ArticulatedLine;
	static inline std::unique_ptr<PolygonEngine> Polygon;

	static void Init();
	static void RenderDrawing(Drawing* drawing);
};

