#pragma once

class LayerManager
{
public:
	std::vector<entt::entity> Layers{};
	std::vector<entt::entity> SelectedLayers{};

	void Draw();
	void AddBack();
};
