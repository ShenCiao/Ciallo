#include "pch.hpp"
#include "Toolbox.h"

#include "CanvasPanel.h"

Toolbox::Toolbox(CanvasPanel* panel)
{
	PaintTool = std::make_unique<class PaintTool>(panel);
	ActiveTool = static_cast<Tool*>(PaintTool.get());
}

void Toolbox::Draw()
{
	ImGui::Begin("Toolbox");

	if(ImGui::Button("Paint"))
	{
		ActiveTool = static_cast<Tool*>(PaintTool.get());
	}

	ImGui::End();
}
