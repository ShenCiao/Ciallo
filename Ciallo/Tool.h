#pragma once

class CanvasPanel;

class Tool
{
public:
	explicit Tool(CanvasPanel* panel);
	CanvasPanel* Panel = nullptr;
	

	virtual void Activate()
	{
	}

	virtual void Deactivate()
	{
	}

	virtual void ClickOrDragStart()
	{
	}

	virtual void Dragging()
	{
	}

	// Run under the invisible button of canvas
	virtual void Run() // world position of canvas
	{
		if (ImGui::IsMouseClicked(0) && ImGui::IsItemActivated())
		{
			ClickOrDragStart();
			return;
		}

		if (ImGui::IsMouseDragging(0) && !ImGui::IsItemActivated() && ImGui::IsItemActive())
		{
			Dragging();
			return;
		}
	}
};
