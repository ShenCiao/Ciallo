#pragma once

#include "BrushManager.h"

class Project
{
public:
	entt::registry* MainRegistry;
	std::unique_ptr<BrushManager> BrushManager;

	Project() = default;
	Project(const Project& other) = delete;
	Project(Project&& other) noexcept = default;
	Project& operator=(const Project& other) = delete;
	Project& operator=(Project&& other) noexcept = default;
	~Project() = default;
};