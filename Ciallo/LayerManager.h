#pragma once

#include <treehh/tree.hh>

#include "Application.h"
#include "Application.h"
#include "Layer.h"

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
	bool Dropped = false;
	tree<entt::entity>::iterator DropTargetIt;
	glm::vec2 DropTargetScreenPos;
	tree<entt::entity>::iterator DragSourceIt;
	enum class InsertionType { None, PreviousSibling, NextSibling, PrependChild, AppendChild };
	InsertionType Insertion = InsertionType::None;
	bool IsRenaming = false;
	void RecursivelyDrawTreeNodes(tree<entt::entity>::iterator it);
	// Usually, a drop target span the two sibling layer nodes, means it drops between them.
	// but when the target position is between a leaf and a parent, there is a conflict. The drop target should be divided into two half.
	enum class DropTargetType { UpperHalf, LowerHalf };
	bool DrawDropTarget(glm::vec2 pos);
public:
	template<class Archive>
	void serialize(Archive& archive)
	{
		archive(LayerTree);
	}
};
