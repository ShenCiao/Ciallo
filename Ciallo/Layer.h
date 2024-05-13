#pragma once

enum class LayerType
{
	Normal,
	Mask,
};

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
	LayerType Type = LayerType::Normal;
	std::string Name = {};
};
