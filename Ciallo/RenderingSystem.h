#pragma once

#include "ArticulatedLineEngine.h"
#include "PolygonEngine.h"
#include "PrefixSumPosition.h"

class RenderingSystem
{
public:
	static inline std::unique_ptr<ArticulatedLineEngine> ArticulatedLine;
	static inline std::unique_ptr<PolygonEngine> Polygon;
	static inline std::unique_ptr<PrefixSumPosition> PrefixSum;

	static void Init();
};

