#include "pch.hpp"
#include "InnerBrush.h"
#include "RenderingSystem.h"
#include "TextureManager.h"

InnerBrush::InnerBrush()
{
	Brush b;
	b.Name = "vanilla";
	b.Program = RenderingSystem::ArticulatedLine->Program();
	Add(std::move(b));

	Brush fillMarker;
	fillMarker.Name = "fill_marker";
	fillMarker.Program = RenderingSystem::ArticulatedLine->Program(ArticulatedLineEngine::Type::Stamp);
	fillMarker.Stamp = std::make_unique<StampBrushData>();
	fillMarker.Stamp->StampTexture = TextureManager::Textures[4];
	fillMarker.Stamp->StampIntervalRatio = 0.75f;
	fillMarker.Stamp->StampMode = StampBrushData::StampMode::RatioDistant;
	fillMarker.Stamp->RotationRand = 0.0f;
	fillMarker.Stamp->NoiseFactor = 0.0f;
	Add(std::move(fillMarker));
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
