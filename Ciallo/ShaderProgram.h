#pragma once

#include "ArticulatedLineShader.h"
#include "PolygonShader.h"
#include "ArticulatedLineComp.h"
#include "DotShader.h"

class ShaderProgram
{
public:
	static inline std::unique_ptr<ArticulatedLineShader> ArticulatedLine;
	static inline std::unique_ptr<PolygonShader> Polygon;
	static inline std::unique_ptr<ArticulatedLineComp> PrefixSum;
	static inline std::unique_ptr<DotShader> Dot;

	static void Init();
};

