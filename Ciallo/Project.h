#pragma once

#include "Drawing.h"
#include "CanvasPanel.h"

class Project
{
public:
	entt::registry Registry;
	std::unique_ptr<Drawing> MainDrawing; // Code a class DrawingManager for here when needed. I'm lazy now.
	std::unique_ptr<CanvasPanel> CanvasPanel;

	Project() = default;
	Project(const Project& other) = delete;
	Project(Project&& other) noexcept = default;
	Project& operator=(const Project& other) = delete;
	Project& operator=(Project&& other) noexcept = default;
	~Project() = default;
};

