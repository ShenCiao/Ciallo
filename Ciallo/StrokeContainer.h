#pragma once
class StrokeContainer
{
public:
	std::vector<entt::entity> StrokeEs{};
};

// Dirtiest thing in this project. It sicks me.
class OverlayContainer
{
public:
	std::list<Geom::Polyline> Lines;
	std::list<Geom::Polyline> Circles;
};