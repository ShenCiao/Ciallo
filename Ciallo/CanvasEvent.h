#pragma once

struct BeforeInteraction{};
struct AfterInteraction{};

struct Hovering
{
	glm::vec2 MousePos{};
	glm::vec2 MousePosPixel{};
};

struct ClickOrDragStart : Hovering
{
};

struct Dragging : ClickOrDragStart
{
	glm::vec2 DeltaMousePos{};
	glm::vec2 DeltaMousePosPixel{};
	chrono::duration<float> DragDuration = chrono::duration<float>::zero();
};

struct DragEnd : Dragging
{
};

