#pragma once

class LayerManager
{
public:
	std::list<entt::entity> Layers{};
	std::unordered_set<entt::entity> SelectedLayers{};

	void Draw();
	void DrawMenuButton();
	entt::entity CreateLayer();
	// move to back if target not found
	void MoveSelection(entt::entity target = entt::null);
	void RemoveSelection();
private:
	bool IsRenaming = false;
	bool IsMultiSelecting() const;
};
