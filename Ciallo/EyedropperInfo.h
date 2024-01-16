#pragma once

// A quick hack to get the eyedropper to work
class EyedropperInfo
{
public:
	bool IsPicking = false;
	glm::vec3 Color{};

	EyedropperInfo() = default;
	~EyedropperInfo() = default;

};