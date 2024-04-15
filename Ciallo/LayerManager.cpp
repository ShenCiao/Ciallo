#include "pch.hpp"
#include "LayerManager.h"

#include "Layer.h"
#include "StrokeContainer.h"
#include <algorithm>
#include <boost/graph/visitors.hpp>
#include <boost/graph/depth_first_search.hpp>

LayerManager::LayerManager()
{
	LayerTree[0] = entt::null; // root node
	auto v0 = boost::add_vertex(LayerTree);
	LayerTree[v0] = R.create();
	boost::add_edge(0, v0, LayerTree);
	auto v1 = boost::add_vertex(LayerTree);
	LayerTree[v1] = R.create();
	boost::add_edge(0, v1, LayerTree);
	auto v2 = boost::add_vertex(LayerTree);
	LayerTree[v2] = R.create();
	boost::add_edge(v0, v2, LayerTree);
}
void LayerManager::DrawUI()
{
	ImGui::Begin("LayerManager", nullptr, ImGuiWindowFlags_MenuBar);
	float framePad = 25.f;
	ImGuiTreeNodeFlags flag = ImGuiTreeNodeFlags_FramePadding | ImGuiTreeNodeFlags_Leaf |
		ImGuiTreeNodeFlags_NoTreePushOnOpen | ImGuiTreeNodeFlags_SpanAvailWidth | ImGuiTreeNodeFlags_AllowItemOverlap;
	ImGui::BeginMenuBar();
	DrawMenuButton();
	ImGui::EndMenuBar();
	ImGui::PushStyleVar(ImGuiStyleVar_FramePadding, ImVec2(0.f, framePad));
	ImGui::Separator();

	auto drawDragTarget = []()
	{
		bool dropped = false;
		glm::vec2 nextStart = ImGui::GetCursorPos();
		ImGui::SetCursorPos(ImGui::GetCursorPos() - ImVec2(0, ImGui::GetFrameHeight() / 2));
		ImGui::Dummy({1000.f, ImGui::GetFrameHeight()});
		ImGui::SetCursorPos(nextStart);
		if (ImGui::BeginDragDropTarget())
		{
			if (const ImGuiPayload* payload = ImGui::AcceptDragDropPayload(
				"LayerMove", ImGuiDragDropFlags_AcceptPeekOnly))
			{
				ImGui::Separator();
				if (payload->IsDelivery())
				{
					dropped = true;
				}
			}
			ImGui::EndDragDropTarget();
		}
		return dropped;
	};
	bool dragMove = false;
	entt::entity dropTarget = entt::null;
	for (auto e : Layers)
	{
		auto& layer = R.get<Layer>(e);
		// Drag target
		if (drawDragTarget())
		{
			dropTarget = e;
			dragMove = true;
		}
		// Visibility checkbox
		ImGui::CheckboxFlags(fmt::format("##vis{}", e).c_str(),
		                     reinterpret_cast<unsigned*>(&layer.Flags),
		                     static_cast<unsigned>(LayerFlags::Visible));
		ImGui::SameLine();
		// Tree node
		ImGuiTreeNodeFlags extraFlag = 0;
		if (SelectedLayers.contains(e))
			extraFlag |= ImGuiTreeNodeFlags_Selected;
		ImGui::TreeNodeEx(fmt::format("##node{}", e).c_str(), flag | extraFlag, "");

		// selection gesture
		// MultiSelGesture is the gesture of the nodes that have been selected.
		bool IsMultiSelGesture = IsMultiSelecting() && SelectedLayers.contains(e);
		if (!IsMultiSelGesture && ImGui::IsItemClicked() && !ImGui::IsDragDropActive() && !IsRenaming)
		{
			if (!ImGui::IsKeyDown(ImGuiKey_LeftCtrl)) SelectedLayers.clear();
			SelectedLayers.insert(e);
			IsRenaming = false;
		}

		// detect release instead of click to avoid conflict with drag and drop
		if (IsMultiSelGesture && ImGui::IsItemClicked() && !ImGui::IsDragDropActive())
		{
			if (ImGui::IsKeyDown(ImGuiKey_LeftCtrl))
			{
				SelectedLayers.erase(e);
			}
			IsRenaming = false;
		}

		if (IsMultiSelGesture && ImGui::IsItemHovered() && ImGui::IsMouseReleased(0) && !ImGui::IsDragDropActive())
		{
			if (!ImGui::IsKeyDown(ImGuiKey_LeftCtrl))
			{
				SelectedLayers.clear();
				SelectedLayers.insert(e);
			}
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
		}

		ImGui::SameLine();
		// // Layer preview, disable it for using infinite canvas
		// float imageRatio = static_cast<float>(layer.Content.Width) / layer.Content.Height;
		// ImGui::Image(reinterpret_cast<ImTextureID>(layer.Content.ColorTexture), {
		// 	             imageRatio * ImGui::GetFrameHeight(), ImGui::GetFrameHeight()
		//              });
		// ImGui::SameLine();
		
		// name or rename
		glm::vec2 nameStart = ImGui::GetCursorPos();
		ImGui::TextUnformatted(layer.Name.c_str());
		if (IsRenaming && !IsMultiSelecting() && SelectedLayers.contains(e))
		{
			ImGui::SetCursorPos(nameStart);
			bool done = ImGui::InputText(fmt::format("##nameInput{}", e).c_str(), &layer.Name,
			                             ImGuiInputTextFlags_EnterReturnsTrue | ImGuiInputTextFlags_AutoSelectAll);
			if (done || ImGui::IsItemDeactivated())
				IsRenaming = false;
		}
	}
	// pad one drop target
	if (drawDragTarget())
		dragMove = true;

	if (dragMove)
		MoveSelection(dropTarget);

	ImGui::PopStyleVar();
	ImGui::End();
}
void LayerManager::Run()
{
	// DFS to get the order of layers
	class DFSVisitor : public boost::default_dfs_visitor
	{
	public:
		std::vector<entt::entity>& Layers;
		DFSVisitor(std::vector<entt::entity>& layers) : Layers(layers) {}
		void discover_vertex(LayerGraph::vertex_descriptor v, const LayerGraph& g)
		{
			Layers.push_back(g[v]);
		}
	};
	std::vector<entt::entity> layers;
	DFSVisitor visitor{layers};
	// Color_map is a just dummy variable used to mark the visited vertices.
	// Why do I need to specify this redundant thing?
	std::vector<boost::default_color_type> color_map(boost::num_vertices(LayerTree));
	boost::depth_first_visit(LayerTree, 0, visitor,
		boost::make_iterator_property_map(color_map.begin(), boost::get(boost::vertex_index, LayerTree))
	);
}

void LayerManager::DrawMenuButton()
{
	if (ImGui::Button("Add"))
	{
		entt::entity e = CreateLayer();
		R.emplace<StrokeContainer>(e);
		Layers.push_back(e);
	}

	if (ImGui::Button("Remove"))
	{
		RemoveSelection();
	}
}

void LayerManager::RemoveSelection()
{
	for (auto it = Layers.begin(); it != Layers.end();)
	{
		if (SelectedLayers.contains(*it))
		{
			it = Layers.erase(it);
		}
		else
		{
			++it;
		}
	}
}

entt::entity LayerManager::CreateLayer() const
{
	entt::entity e = R.create();
	auto& l = R.emplace<Layer>(e);
	l.Name.reserve(128);
	l.Name = fmt::format("Layer {}", boost::num_vertices(LayerTree));
	return e;
}
void LayerManager::GenChildLayer(entt::entity parent)
{
	
}

void LayerManager::MoveSelection(entt::entity target)
{
	std::vector<entt::entity> selInOrder;

	auto insertIt = std::find(Layers.begin(), Layers.end(), target);
	while (insertIt != Layers.end() && SelectedLayers.contains(*insertIt))
	{
		++insertIt;
	}

	for (auto it = Layers.begin(); it != Layers.end();)
	{
		if (SelectedLayers.contains(*it))
		{
			selInOrder.push_back(*it);
			it = Layers.erase(it);
		}
		else
		{
			++it;
		}
	}

	Layers.insert(insertIt, selInOrder.begin(), selInOrder.end());
}

bool LayerManager::IsMultiSelecting() const
{
	return SelectedLayers.size() > 1;
}
