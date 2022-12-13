#include "pch.hpp"
#include "LayerManager.h"

#include "Layer.h"
#include <algorithm>

void LayerManager::Draw()
{
	ImGui::Begin("LayerManager");
	ImGui::PushStyleVar(ImGuiStyleVar_FramePadding, ImVec2(50.f, 50.f));
	ImGuiTreeNodeFlags flag = ImGuiTreeNodeFlags_FramePadding | ImGuiTreeNodeFlags_Leaf |
		ImGuiTreeNodeFlags_SpanFullWidth;
	// ImGui::beginM
	for (entt::entity e : Layers)
	{
		ImGuiTreeNodeFlags extra = 0;
		if (std::find(SelectedLayers.begin(), SelectedLayers.end(), e) != SelectedLayers.end())
		{
			extra = ImGuiTreeNodeFlags_Selected;
		}
		auto& layer = R.get<Layer>(e);
		ImGui::Text("test");
		ImGui::SameLine();
		ImGui::TreeNodeEx(reinterpret_cast<void*>(e), flag | extra, layer.Name.c_str());
		
	}

	ImGui::PopStyleVar();
	ImGui::End();
}

void LayerManager::AddBack()
{
	entt::entity e = R.create();
	auto& l = R.emplace<Layer>(e);
	l.Name = "Test";
	Layers.push_back(e);
}
