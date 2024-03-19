#pragma once

// Store user specified global states.
class SelectionManager
{
public:
	entt::entity StrokeContainerE = entt::null; // Selected entity with stroke container.
	std::vector<entt::entity> StrokeEs{}; // Selected strokes.
};
