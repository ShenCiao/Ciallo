#include "pch.hpp"
#include "InnerBrush.h"
#include "RenderingSystem.h"

InnerBrush::InnerBrush()
{
	Brush b;
	b.Name = "vanilla";
	b.Program = RenderingSystem::ArticulatedLine->Program();
	Add(std::move(b));
}

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
