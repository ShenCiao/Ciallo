#pragma once

#include "RenderableTexture.h"
#include "AirBrushData.h"
#include "StampBrushData.h"

class Brush
{
public:
	// Program are owned by other managers;
	GLuint Program = 0;
	
	std::string Name;
	RenderableTexture PreviewTexture{};

	std::unique_ptr<AirBrushData> AirBrush{};
	std::unique_ptr<StampBrushData> Stamp{};

	void SetUniform();
	void Use();
};
