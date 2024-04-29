#pragma once

// A bad design for historical reasons.
class ArticulatedLineShader
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

	ArticulatedLineShader();
	~ArticulatedLineShader();

	GLuint Program(Type type = Type::Vanilla);
};
