#pragma once

// Store user specified global states.
class Context
{
public:
	entt::entity SelectedStrokeContainer;
	std::vector<entt::entity> CurrentStrokes;
};
