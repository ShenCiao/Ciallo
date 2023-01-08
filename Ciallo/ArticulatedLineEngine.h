#pragma once

class ArticulatedLineEngine
{
public:
	enum class Type
	{
		Vanilla = 0,
		Stamp = 1 << 0,
		Airbrush = 1 << 1,
		_entt_enum_as_bitmask
	};

	std::unordered_map<Type, GLuint> Programs;

	ArticulatedLineEngine();
	~ArticulatedLineEngine();

	GLuint Program(Type type = Type::Vanilla);
};
