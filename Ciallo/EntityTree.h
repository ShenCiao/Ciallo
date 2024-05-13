#pragma once

#include <deque>

class EntityNode;
using NodeChildren = std::vector<EntityNode>;

class EntityNode
{
public:
	entt::entity Value = entt::null;
	NodeChildren Children;
};

class EntityTree
{
public:
	EntityNode Root;
};
