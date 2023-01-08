#pragma once

class LayerManager
{
public:
	std::list<entt::entity> Layers{};
	std::unordered_set<entt::entity> SelectedLayers{};

	void DrawUI();
	void DrawMenuButton();
	entt::entity CreateLayer() const;
	// move to back if target not found
	void MoveSelection(entt::entity target = entt::null);
	void RemoveSelection();
private:
	bool IsRenaming = false;
	bool IsMultiSelecting() const;
};
