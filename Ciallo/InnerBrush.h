#pragma once
#include "Brush.h"

class InnerBrush
{
	std::unordered_map<std::string, Brush> Brushes{};
public:
	void Add(Brush&& brush);
	Brush& Get(const std::string& name);
};

