#pragma once
class StrokeContainer
{
public:
	StrokeContainer();

	std::vector<entt::entity> StrokeEs{};

	template<class Archive>
	void serialize(Archive& archive)
	{
		archive(StrokeEs);
	}
	template<class Archive>
	void epilogue(Archive& archive)
	{
	}
};

// 2023 March. Dirtiest thing in this project. It sicks me.
// Well, there are more dirty things sadly.
class OverlayContainer
{
public:
	std::list<Geom::Polyline> Lines;
	std::list<Geom::Polyline> Circles;
};