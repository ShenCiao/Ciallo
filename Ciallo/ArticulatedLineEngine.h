#pragma once

class ArticulatedLineEngine
{
public:
	enum class Type
	{
		Vanilla = 0,
		Stamp = 1 << 0, //move to left with 0 step - output: 1
		Airbrush = 1 << 1, //move to left with 0 step - output: 2
		_entt_enum_as_bitmask
	};

	std::unordered_map<Type, GLuint> Programs;

	ArticulatedLineEngine();
	~ArticulatedLineEngine();

	GLuint Program(Type type = Type::Vanilla);
};
