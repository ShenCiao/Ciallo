#pragma once

class Project
{
public:
	entt::registry Registry;

	Project() = default;
	Project(const Project& other) = delete;
	Project(Project&& other) noexcept = default;
	Project& operator=(const Project& other) = delete;
	Project& operator=(Project&& other) noexcept = default;
	~Project() = default;
};

