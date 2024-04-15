#pragma once

#include <boost/graph/adjacency_list.hpp>

using LayerGraph = boost::adjacency_list<boost::vecS, boost::vecS, boost::directedS, entt::entity>;

// Layers are unused in this project
class LayerManager
{
public:
	LayerManager();
	LayerGraph LayerTree = LayerGraph(1); // reserve a place for root node
	std::vector<entt::entity> Layers;
	std::unordered_set<entt::entity> SelectedLayers{};

	void DrawUI();
	void Run();
	void DrawMenuButton();
	entt::entity CreateLayer() const;
	void GenChildLayer(entt::entity parent);
	// move to back if target not found
	void MoveSelection(entt::entity target = entt::null);
	void RemoveSelection();
private:
	bool IsRenaming = false;
	bool IsMultiSelecting() const;
};
