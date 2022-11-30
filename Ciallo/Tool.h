#pragma once

class CanvasPanel;

class Tool
{
protected:
	chrono::time_point<chrono::steady_clock> StartDraggingTimePoint{};
	chrono::duration<float, std::milli> DraggingDuration;
	bool IsDragging = false;
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

	virtual void DragEnd()
	{
	}

	virtual void DrawProperties()
	{
	}

	// Run under the invisible button of canvas, default behavior
	virtual void Run();
};
