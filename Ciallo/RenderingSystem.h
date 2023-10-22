#pragma once

#include "ArticulatedLineEngine.h"
#include "PolygonEngine.h"
#include "ArticulatedLineComp.h"
#include "DotEngine.h"

class RenderingSystem
{
public:
	static inline std::unique_ptr<ArticulatedLineEngine> ArticulatedLine;
	static inline std::unique_ptr<PolygonEngine> Polygon;
	static inline std::unique_ptr<ArticulatedLineComp> PrefixSum;
	static inline std::unique_ptr<DotEngine> Dot;

	static void Init();
};

