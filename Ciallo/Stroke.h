#pragma once

class Stroke
{
	Geom::Polyline mPosition{};
	std::vector<float> mWidth{};
	entt::entity mBrush = entt::null;
public:
	Stroke() = default;
};

