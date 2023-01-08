#include "pch.hpp"
#include "Tool.h"


void Tool::Connect(entt::dispatcher& dispatcher)
{
	dispatcher.sink<ClickOrDragStart>().connect<&Tool::OnClickOrDragStart>(this);
	dispatcher.sink<Dragging>().connect<&Tool::OnDragging>(this);
	dispatcher.sink<DragEnd>().connect<&Tool::OnDragEnd>(this);
	dispatcher.sink<Hovering>().connect<&Tool::OnHovering>(this);
}

void Tool::Disconnect(entt::dispatcher& dispatcher)
{
	dispatcher.sink<ClickOrDragStart>().disconnect<&Tool::OnClickOrDragStart>(this);
	dispatcher.sink<Dragging>().disconnect<&Tool::OnDragging>(this);
	dispatcher.sink<DragEnd>().disconnect<&Tool::OnDragEnd>(this);
	dispatcher.sink<Hovering>().disconnect<&Tool::OnHovering>(this);
}
