#include "pch.hpp"
#include "LayerManager.h"

#include "StrokeContainer.h"

LayerManager::LayerManager()
{
	LayerTree.set_head(entt::null);
	SelectedLayerIt = LayerTree.begin();
	auto t = LayerTree.append_child(LayerTree.begin(), CreateLayer());
	LayerTree.append_child(LayerTree.begin(), CreateLayer());
	LayerTree.append_child(t, CreateLayer());
	t = LayerTree.append_child(t, CreateLayer());
	t = LayerTree.append_child(t, CreateLayer());
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
	Insertion = InsertionType::None;
	RecursivelyDrawTreeNodes(LayerTree.begin());
	// Pad a final drop target
	if (DrawDropTarget(DropTargetType::UpperHalf))
	{
		DropTargetIt = LayerTree.begin();
		Insertion = InsertionType::AppendChild;
	}

	tree<entt::entity>::iterator tempIt;
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
		tempIt = LayerTree.append_child(DragSourceIt, entt::null);
		LayerTree.move_ontop(tempIt, DragSourceIt);
		break;
	case InsertionType::AppendChild:
		tempIt = LayerTree.append_child(DragSourceIt, entt::null);
		LayerTree.move_ontop(tempIt, DragSourceIt);
		break;
	}

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
		LayerTree.append_child(LayerTree.begin(), e);
		// else
		// 	LayerTree.insert_after(SelectedLayerIt, e);
	}

	if (ImGui::Button("Add Mask"))
	{
		entt::entity e = CreateLayer(LayerType::Mask);
		// if (SelectedLayerIt == LayerTree.begin())
		LayerTree.append_child(LayerTree.begin(), e);
		// else
		// 	LayerTree.insert_after(SelectedLayerIt, e);
	}

	if (ImGui::Button("Remove"))
	{
		LayerTree.erase(SelectedLayerIt);
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
	bool isNodeOpen = true;
	if (!isRootNode)
	{
		// Draw drop targets, make sure call these functions before checkbox
		if (DrawDropTarget(DropTargetType::UpperHalf))
		{
			DropTargetIt = it;
			Insertion = InsertionType::PreviousSibling;
		}
		if (DrawDropTarget(DropTargetType::LowerHalf))
		{
			DropTargetIt = it;
			Insertion = isNodeOpen ? InsertionType::PrependChild : InsertionType::NextSibling;
		}
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
		if (layer.Type == LayerType::Normal)
			flag |= ImGuiTreeNodeFlags_Leaf;
		isNodeOpen = ImGui::TreeNodeEx(fmt::format("##node{}", e).c_str(), flag, "");

		// selection gesture
		if (ImGui::IsItemClicked() && !ImGui::IsDragDropActive() && !IsRenaming)
		{
			SelectedLayerIt = it;
			IsRenaming = false;
		}

		// renaming gesture
		if (ImGui::IsItemHovered() && ImGui::IsMouseDoubleClicked(0) && !IsRenaming)
			IsRenaming = true;

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
			bool done = ImGui::InputText(fmt::format("##nameInput{}", e).c_str(), &layer.Name,
			                             ImGuiInputTextFlags_EnterReturnsTrue | ImGuiInputTextFlags_AutoSelectAll);
			if (done || ImGui::IsItemDeactivated())
				IsRenaming = false;
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
bool LayerManager::DrawDropTarget(DropTargetType type, bool indent)
{
	bool dropped = false;
	glm::vec2 nextPos = ImGui::GetCursorPos(); // start position of the next tree node.

	if (type == DropTargetType::LowerHalf)
		ImGui::SetCursorPos(nextPos + glm::vec2(0, ImGui::GetFrameHeightWithSpacing() / 2));

	ImGui::Dummy({1000.f, ImGui::GetFrameHeight() / 2});
	ImGui::SetCursorPos(nextPos);
	if (indent) ImGui::Indent();
	glm::vec2 screenPos = ImGui::GetCursorScreenPos();
	if (indent) ImGui::Unindent();
	if (ImGui::BeginDragDropTarget())
	{
		if (const ImGuiPayload* payload = ImGui::AcceptDragDropPayload(
			"LayerMove", ImGuiDragDropFlags_AcceptPeekOnly))
		{
			float lineThickness = 2.0f;
			auto* drawList = ImGui::GetWindowDrawList();
			glm::vec2 pos;
			if (type == DropTargetType::UpperHalf)
				pos = screenPos - glm::vec2(0.0, lineThickness / 2);
			else
				pos = screenPos + glm::vec2(0.0, ImGui::GetFrameHeightWithSpacing()) - glm::vec2(0.0, lineThickness / 2);

			drawList->AddLine(pos, pos + glm::vec2(1000.f, 0), IM_COL32(240, 240, 210, 255), lineThickness);
			dropped = payload->IsDelivery();
		}
		ImGui::EndDragDropTarget();
	}
	return dropped;
}
