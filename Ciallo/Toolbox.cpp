#include "pch.hpp"
#include "Toolbox.h"

#include "PaintTool.h"
#include "EditTool.h"
#include "FillTool.h"
#include "Canvas.h"

Toolbox::Toolbox()
{
	auto paintTool = std::make_unique<PaintTool>();
	ActiveTool = paintTool.get();
	auto& canvas = R.ctx().get<Canvas>();
	ActiveTool->Connect(canvas.EventDispatcher);
	ActiveTool->Activate();
	Tools.push_back(std::move(paintTool));

	auto editTool = std::make_unique<EditTool>();
	Tools.push_back(std::move(editTool));

	auto fillTool = std::make_unique<FillTool>();
	Tools.push_back(std::move(fillTool));
}

void Toolbox::DrawUI()
{
	ImGui::Begin("Toolbox");
	Tool* prevActive = ActiveTool;
	for (auto& tool : Tools)
	{
		if (ImGui::Selectable(tool->GetName().c_str(), ActiveTool == tool.get()))
			ActiveTool = tool.get();
	}
	
	if (prevActive != ActiveTool)
	{
		auto& canvas = R.ctx().get<Canvas>();
		prevActive->Deactivate();
		prevActive->Disconnect(canvas.EventDispatcher);
		ActiveTool->Connect(canvas.EventDispatcher);
		ActiveTool->Activate();
	}
	ImGui::Separator();
	ActiveTool->DrawProperties();
	ImGui::End();
}
