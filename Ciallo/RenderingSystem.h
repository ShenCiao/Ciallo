#pragma once

#include <ranges>

#include "ArticulatedLineEngine.h"
#include "Drawing.h"

class RenderingSystem
{
public:
	ArticulatedLineEngine ArticulatedLine;

	void RenderDrawing(Drawing* drawing);
};

