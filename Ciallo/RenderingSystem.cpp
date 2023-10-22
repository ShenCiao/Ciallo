#include "pch.hpp"
#include "RenderingSystem.h"


void RenderingSystem::Init()
{
	ArticulatedLine = std::make_unique<ArticulatedLineEngine>();
	Polygon = std::make_unique<PolygonEngine>();
	PrefixSum = std::make_unique<ArticulatedLineComp>();
	Dot = std::make_unique<DotEngine>();
}