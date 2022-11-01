#pragma once

class Stroke
{
	Geom::Polyline Position{};
	std::vector<float> Width{};
	entt::entity Brush = entt::null;
public:
	Stroke() = default;
};

