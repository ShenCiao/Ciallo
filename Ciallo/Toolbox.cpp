#include "pch.hpp"
#include "Toolbox.h"

#include "PaintTool.h"
#include "EditTool.h"
#include "FillTool.h"
#include "Canvas.h"

Toolbox::Toolbox()
{
	Tools.push_back(R.create());
	auto& paintTool = R.emplace<std::unique_ptr<Tool>>(Tools.back(), std::make_unique<PaintTool>());
	ActiveTool = Tools.back();
	auto& canvas = R.ctx().get<Canvas>();
	paintTool->Connect(canvas.EventDispatcher);
	paintTool->Activate();

	Tools.push_back(R.create());
	R.emplace<std::unique_ptr<Tool>>(Tools.back(), std::make_unique<EditTool>());

	Tools.push_back(R.create());
	R.emplace<std::unique_ptr<Tool>>(Tools.back(), std::make_unique<FillTool>());
}

void Toolbox::DrawUI()
{
	ImGui::Begin("Toolbox");
	entt::entity prevActive = ActiveTool;
	for(auto toolE: Tools)
	{
		auto& tool = R.get<std::unique_ptr<Tool>>(toolE);
		if (ImGui::Selectable(tool->GetName().c_str(), ActiveTool == toolE))
            ActiveTool = toolE;
	}

	if(prevActive != ActiveTool)
	{
		auto& canvas = R.ctx().get<Canvas>();
		auto& prevTool = R.get<std::unique_ptr<Tool>>(prevActive);
		auto& tool = R.get<std::unique_ptr<Tool>>(ActiveTool);
		prevTool->Deactivate();
		prevTool->Disconnect(canvas.EventDispatcher);
		tool->Connect(canvas.EventDispatcher);
		tool->Activate();
	}
	ImGui::End();
}