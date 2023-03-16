#pragma once
class StrokeContainer
{
public:
	std::vector<entt::entity> StrokeEs{};
};

class OverlayContainer
{
public:
	std::list<Geom::Polyline> Lines;
	std::list<Geom::Polyline> Circles;
};