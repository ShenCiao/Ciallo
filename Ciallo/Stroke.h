#pragma once

class Stroke
{
	Geom::Polyline mPosition{};
	std::vector<float> mWidth{};
	entt::entity mBrush;
public:
	Stroke() = default;
};

