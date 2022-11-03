#include "pch.hpp"
#include "EntityObject.h"

EntityObject::EntityObject(): entt::handle(*Registry, Registry->create())
{
	emplace<EntityObject*>(this);
}

EntityObject::EntityObject(const EntityObject& other): EntityObject()
{

}

EntityObject::EntityObject(EntityObject&& other) noexcept: entt::handle(*Registry, other.entity())
{
	static_cast<entt::handle&>(other) = { *Registry, entt::null };
	replace<EntityObject*>(this);
}

EntityObject& EntityObject::operator=(EntityObject other)
{
	swap(*this, other);
	return *this;
}

EntityObject::~EntityObject()
{
	if (entity() != entt::null)
		Registry->destroy(entity());
}

void swap(EntityObject& lhs, EntityObject& rhs) noexcept
{
	using std::swap;
	swap(static_cast<entt::handle&>(lhs), static_cast<entt::handle&>(rhs));
	swap(lhs.get<EntityObject*>(), rhs.get<EntityObject*>());
}
