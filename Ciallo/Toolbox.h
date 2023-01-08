#pragma once

class Toolbox
{
	entt::entity ActiveTool;
	std::vector<entt::entity> Tools;
public:
	Toolbox();
	void DrawUI();
};

