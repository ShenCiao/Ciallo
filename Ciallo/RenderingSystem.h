#pragma once

#include "ArticulatedLineEngine.h"
#include "Drawing.h"

class RenderingSystem
{
public:
	static inline std::unique_ptr<ArticulatedLineEngine> ArticulatedLine;

	static void Init();
	static void RenderDrawing(Drawing* drawing);
};

