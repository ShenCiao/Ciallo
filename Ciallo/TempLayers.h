#pragma once

// Used to catch up the siggraph DDL
class TempLayers
{
public:
	std::array<entt::entity, 3> Layers;

	TempLayers();
	
	entt::entity GetOverlay() const;
	entt::entity GetDrawing() const;
	entt::entity GetFill() const;
};

