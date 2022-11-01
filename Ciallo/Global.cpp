#include "pch.hpp"
#include "Global.h"

entt::registry* r;

entt::registry& GetRegistry()
{
	return *r;
}

void SetRegistry(entt::registry& registry)
{
	r = &registry;
}