#include "pch.hpp"
#include "ShaderProgram.h"


void ShaderProgram::Init()
{
	ArticulatedLine = std::make_unique<ArticulatedLineShader>();
	Polygon = std::make_unique<PolygonShader>();
	PrefixSum = std::make_unique<ArticulatedLineComp>();
	Dot = std::make_unique<DotShader>();
}