#pragma once
/*
 * RAII wrapper of entt::handle
 * I thought ECS was useful. I could attach any components to an entity object. The component data is not intrusive to the entity object, which avoids "god class".
 * But I'm working alone, and far far away from a single "god class" (it's definitely the class `Stroke` in the future").
 * So I'm not going to use the ECS until I get some teammates. Let the smart pointer and oop rule everything.
 */
class EntityObject : entt::handle
{
public:
	static inline entt::registry* Registry;
	EntityObject();
	EntityObject(const EntityObject& other);
	EntityObject(EntityObject&& other) noexcept;
	EntityObject& operator=(EntityObject other);
	~EntityObject();

	friend void swap(EntityObject& lhs, EntityObject& rhs) noexcept;

	uint32_t GetId() const
	{
		return static_cast<uint32_t>(entity());
	}

	entt::handle GetHandle() const
	{
		return {*registry(), entity()};
	}
};
