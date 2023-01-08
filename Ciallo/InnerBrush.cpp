#include "pch.hpp"
#include "InnerBrush.h"

void InnerBrush::Add(Brush&& brush)
{
	// pitfall :(
	std::string key = brush.Name;
	Brushes[key] = std::move(brush);
}

Brush& InnerBrush::Get(const std::string& name)
{
	return Brushes.at(name);
}
