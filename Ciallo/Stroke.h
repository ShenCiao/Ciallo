#pragma once

class Stroke
{
public:
	std::vector<Geom::Point> Position{}; // This is supposed to be a Geom::Polyline class. But I'm not going to make extra unnecessary stuff for now.
	std::vector<float> Width{};
	entt::entity Brush = entt::null;

	Stroke() = default;
	
};

