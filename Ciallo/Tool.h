#pragma once

class CanvasPanel;

class Tool
{
protected:
	chrono::time_point<chrono::steady_clock> StartDraggingTimePoint{};
	chrono::duration<float, std::milli> DraggingDuration;
public:
	explicit Tool(CanvasPanel* canvas);
	CanvasPanel* Canvas = nullptr;

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

	// Run under the invisible button of canvas, default behavior
	virtual void Run()
	{
		if (ImGui::IsMouseClicked(0) && ImGui::IsItemActivated())
		{
			StartDraggingTimePoint = std::chrono::high_resolution_clock::now();
			ClickOrDragStart();
			return;
		}

		if (ImGui::IsMouseDragging(0) && !ImGui::IsItemActivated() && ImGui::IsItemActive())
		{
			DraggingDuration = chrono::high_resolution_clock::now() - StartDraggingTimePoint;
			Dragging();
			return;
		}
	}
};
