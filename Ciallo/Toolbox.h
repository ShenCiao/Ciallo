#pragma once

#include "Tool.h"

class Toolbox
{
public:
	Tool* ActiveTool;
	std::vector<std::unique_ptr<Tool>> Tools{};

	Toolbox();
	~Toolbox() = default;
	Toolbox(const Toolbox& other) = delete;
	Toolbox& operator=(Toolbox& other) = delete;
	Toolbox(Toolbox&& other) noexcept = default;
	Toolbox& operator=(Toolbox&& other) noexcept = default;
	void DrawUI();
};

