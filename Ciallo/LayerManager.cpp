#include "pch.hpp"
#include "LayerManager.h"

#include "StrokeContainer.h"
#include "SerializeTreehh.h"

LayerManager::LayerManager()
{
	LayerTree.set_head(entt::null);
	SelectedLayerIt = LayerTree.begin();
}
void LayerManager::DrawUI()
{
	ImGui::Begin("LayerManager", nullptr, ImGuiWindowFlags_MenuBar);
	float framePad = 25.f;

	ImGui::BeginMenuBar();
	DrawMenuButton();
	ImGui::EndMenuBar();
	ImGui::PushStyleVar(ImGuiStyleVar_FramePadding, ImVec2(0.f, framePad));
	ImGui::PushStyleVar(ImGuiStyleVar_IndentSpacing, ImGui::GetFrameHeight() / 2);
	ImGui::Separator();

	DropTargetIt = LayerTree.end();

	RecursivelyDrawTreeNodes(LayerTree.begin());
	// Pad a final drop target
	if (DrawDropTarget(ImGui::GetCursorPos()))
	{
		DropTargetIt = LayerTree.begin();
		Insertion = InsertionType::AppendChild;
		DropTargetScreenPos = ImGui::GetCursorScreenPos();
	}

	// draw indicator line.
	float lineThickness = 2.f;
	glm::vec2 lineStartScreenPos = DropTargetScreenPos;

	switch (Insertion)
	{
	case InsertionType::None:
		break;
	case InsertionType::PreviousSibling:
		lineStartScreenPos -= glm::vec2(0.0, lineThickness / 2);
		break;
	case InsertionType::NextSibling:
		lineStartScreenPos += glm::vec2(0.0, ImGui::GetFrameHeightWithSpacing() - lineThickness / 2);
		break;
	case InsertionType::PrependChild:
		lineStartScreenPos += glm::vec2(ImGui::GetStyle().IndentSpacing, ImGui::GetFrameHeightWithSpacing() - lineThickness / 2);
		break;
	case InsertionType::AppendChild:
		lineStartScreenPos -= glm::vec2(0.0, lineThickness / 2);
		break;
	}

	if (Insertion != InsertionType::None)
	{
		ImGui::GetWindowDrawList()->AddLine(lineStartScreenPos, lineStartScreenPos + glm::vec2(1000.f, 0),
		                                    IM_COL32(240, 240, 210, 255), lineThickness);
	}

	// Move layers
	tree<entt::entity>::iterator tempIt;
	if (Dropped)
	{
		switch (Insertion)
		{
		case InsertionType::None:
			break;
		case InsertionType::PreviousSibling:
			LayerTree.move_before(DropTargetIt, DragSourceIt);
			break;
		case InsertionType::NextSibling:
			LayerTree.move_after(DropTargetIt, DragSourceIt);
			break;
		case InsertionType::PrependChild:
			tempIt = LayerTree.prepend_child(DropTargetIt, entt::null);
			LayerTree.move_before(tempIt, DragSourceIt);
			LayerTree.erase(tempIt);
			break;
		case InsertionType::AppendChild:
			tempIt = LayerTree.append_child(DropTargetIt, entt::null);
			LayerTree.move_after(tempIt, DragSourceIt);
			LayerTree.erase(tempIt);
			break;
		}
	}
	Insertion = InsertionType::None;

	ImGui::PopStyleVar();
	ImGui::PopStyleVar();
	ImGui::End();
}
void LayerManager::Run()
{
}

void LayerManager::DrawMenuButton()
{
	if (ImGui::Button("Add"))
	{
		entt::entity e = CreateLayer();
		// if (SelectedLayerIt == LayerTree.begin())
		SelectedLayerIt = LayerTree.append_child(LayerTree.begin(), e);
		// else
		// 	LayerTree.insert_after(SelectedLayerIt, e);
	}

	if (ImGui::Button("Add Mask"))
	{
		entt::entity e = CreateLayer(LayerType::Mask);
		// if (SelectedLayerIt == LayerTree.begin())
		SelectedLayerIt = LayerTree.append_child(LayerTree.begin(), e);
		// else
		// 	LayerTree.insert_after(SelectedLayerIt, e);
	}

	if (ImGui::Button("Remove"))
	{
		LayerTree.erase(SelectedLayerIt);
		SelectedLayerIt = LayerTree.begin();
	}
}

entt::entity LayerManager::CreateLayer(LayerType type) const
{
	entt::entity e = R.create();
	auto& l = R.emplace<Layer>(e);
	l.Name.reserve(128);
	l.Name = fmt::format("Layer {}", LayerTree.size());
	l.Type = type;
	R.emplace<StrokeContainer>(e);
	return e;
}

// I tend to create this function as a lambda function. But lambda function doesn't support recursion. 
void LayerManager::RecursivelyDrawTreeNodes(tree<entt::entity>::iterator it)
{
	// Draw current node
	entt::entity e = *it;
	bool isRootNode = e == entt::null;
	bool isNodeOpen = false;
	bool isLeaf = false;
	if (!isRootNode)
	{
		glm::vec2 nodePos = ImGui::GetCursorPos();
		glm::vec2 nodeScreenPos = ImGui::GetCursorScreenPos();

		// Draw tree node
		auto& layer = R.get<Layer>(e);
		ImGui::CheckboxFlags(fmt::format("##vis{}", e).c_str(),
		                     reinterpret_cast<unsigned*>(&layer.Flags),
		                     static_cast<unsigned>(LayerFlags::Visible));
		ImGui::SameLine();
		ImGuiTreeNodeFlags flag = ImGuiTreeNodeFlags_FramePadding | ImGuiTreeNodeFlags_SpanAvailWidth | ImGuiTreeNodeFlags_AllowItemOverlap |
		ImGuiTreeNodeFlags_OpenOnArrow;
		if (*SelectedLayerIt == e)
			flag |= ImGuiTreeNodeFlags_Selected;
		isLeaf = layer.Type == LayerType::Normal;
		if (isLeaf)
			flag |= ImGuiTreeNodeFlags_Leaf;
		isNodeOpen = ImGui::TreeNodeEx(fmt::format("##node{}", e).c_str(), flag, "");

		// selection gesture
		if (ImGui::IsItemClicked() && !ImGui::IsDragDropActive())
		{
			SelectedLayerIt = it;
			IsRenaming = false;
		}

		// renaming gesture
		if (ImGui::IsItemHovered() && ImGui::IsMouseClicked(ImGuiMouseButton_Right))
		{
			SelectedLayerIt = it;
			IsRenaming = true;
		}

		// drag
		if (ImGui::BeginDragDropSource(ImGuiDragDropFlags_None))
		{
			ImGui::SetDragDropPayload("LayerMove", nullptr, 0);
			ImGui::TextUnformatted(layer.Name.c_str());
			ImGui::EndDragDropSource();
			DragSourceIt = it;
			IsRenaming = false;
		}

		ImGui::SameLine();

		// name or rename
		glm::vec2 nameStart = ImGui::GetCursorPos();
		ImGui::TextUnformatted(layer.Name.c_str());

		if (IsRenaming && *SelectedLayerIt == e)
		{
			ImGui::SetCursorPos(nameStart);
			ImGui::SetKeyboardFocusHere(0);
			bool done = ImGui::InputText(fmt::format("##nameInput{}", e).c_str(), &layer.Name,
			                             ImGuiInputTextFlags_EnterReturnsTrue | ImGuiInputTextFlags_AutoSelectAll);

			if (done || ImGui::IsItemDeactivated())
				IsRenaming = false;
		}

		// Draw drop targets
		// upper half
		if (DrawDropTarget(nodePos))
		{
			DropTargetIt = it;
			DropTargetScreenPos = nodeScreenPos;
			Insertion = InsertionType::PreviousSibling;
		}
		// lower half
		if (DrawDropTarget(nodePos + glm::vec2(0, ImGui::GetFrameHeightWithSpacing() / 2)))
		{
			DropTargetIt = it;
			DropTargetScreenPos = nodeScreenPos;
			// Carefully, ImGui allows non leaf nodes to be opened.
			Insertion = isNodeOpen && !isLeaf ? InsertionType::PrependChild : InsertionType::NextSibling;
		}
	}

	if (isNodeOpen || isRootNode)
	{
		// Draw children
		for (auto childrenIt = LayerTree.begin(it); childrenIt != LayerTree.end(it); ++childrenIt)
		{
			RecursivelyDrawTreeNodes(childrenIt);
		}
	}
	if (isNodeOpen && !isRootNode) ImGui::TreePop();
}
bool LayerManager::DrawDropTarget(glm::vec2 pos)
{
	bool acceptingPayload = false;
	glm::vec2 originalPos = ImGui::GetCursorPos(); // Avoid change original cursor position
	ImGui::SetCursorPos(pos);

	ImGui::Dummy({1000.f, ImGui::GetFrameHeightWithSpacing() / 2});

	if (ImGui::BeginDragDropTarget())
	{
		const ImGuiPayload* payload = ImGui::AcceptDragDropPayload(
			"LayerMove", ImGuiDragDropFlags_AcceptPeekOnly);
		acceptingPayload = payload;
		if (acceptingPayload)
		{
			Dropped = payload->IsDelivery();
		}
		ImGui::EndDragDropTarget();
	}
	ImGui::SetCursorPos(originalPos);
	return acceptingPayload;
}
