#pragma once

#include <treehh/tree.hh>
#include "Layer.h"
#include "EntityTree.h"

// This class is only used with Illustration
class LayerManager
{
public:
	LayerManager();
	tree<entt::entity> LayerTree;
	tree<entt::entity>::iterator SelectedLayerIt; // default value (no selection) is LayerTree.begin()

	void DrawUI();
	void Run();
	void DrawMenuButton();
	entt::entity CreateLayer(LayerType = LayerType::Normal) const;
	// move to back if target not found
private:
	tree<entt::entity>::iterator DropTargetIt;
	tree<entt::entity>::iterator DragSourceIt;
	enum class InsertionType { None, PreviousSibling, NextSibling, PrependChild, AppendChild };
	InsertionType Insertion;
	bool IsRenaming = false;
	void RecursivelyDrawTreeNodes(tree<entt::entity>::iterator it);
	// Usually, a drop target span the two sibling layer nodes, means it drops between them.
	// but when the target position is between a leaf and a parent, there is a conflict. The drop target should be divided into two halfs.
	enum class DropTargetType { UpperHalf, LowerHalf };
	static bool DrawDropTarget(DropTargetType, bool indent = false);
};
