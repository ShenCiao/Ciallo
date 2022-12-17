#pragma once

#include "RenderableTexture.h"

class Layer
{
public:
	enum class Flags
	{
		Visible = 1 << 1,
		_entt_enum_as_bitmask
	};

public:
	Flags Flags = Flags::Visible;
	std::string Name;
	RenderableTexture Content;
};
