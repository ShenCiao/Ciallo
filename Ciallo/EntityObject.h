#pragma once

// This may be useful someday?
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
