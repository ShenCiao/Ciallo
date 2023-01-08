#include "pch.hpp"
#include "TempLayers.h"

#include "Layer.h"

TempLayers::TempLayers()
{
	for (int i = 0; i < 3; ++i)
	{
		entt::entity e = R.create();
		Layers[i] = e;
		auto& layer = R.emplace<Layer>(e);
	}
}

entt::entity TempLayers::GetOverlay() const
{
	return Layers[0];
}

entt::entity TempLayers::GetDrawing() const
{
	return Layers[1];
}

entt::entity TempLayers::GetFill() const
{
	return Layers[2];
}
