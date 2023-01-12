#pragma once

#include "RenderableTexture.h"

// Layers are used in this project since siggraph ddl
enum class LayerFlags
	{
		Visible = 1u << 1,
		Selectable = 1 << 2,
		_entt_enum_as_bitmask
	};

class Layer
{
public:
	LayerFlags Flags = LayerFlags::Visible;
	std::string Name;
	RenderableTexture Content;
};
