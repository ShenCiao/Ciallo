#pragma once
#include "CanvasEvent.h"

class Tool
{
public:
	virtual void Activate()
	{
	}

	virtual void Deactivate()
	{
	}

	virtual void DrawProperties()
	{
	}

	virtual void OnClickOrDragStart(ClickOrDragStart)
	{
	}

	virtual void OnDragging(Dragging)
	{
	}

	virtual void OnDragEnd(DragEnd)
	{
	}

	virtual void OnHovering(Hovering)
	{
	}

	Tool() = default;
	virtual ~Tool() = default;

	virtual std::string GetName() = 0;
	
	void Connect(entt::dispatcher& dispatcher);
	void Disconnect(entt::dispatcher& dispatcher);
};
