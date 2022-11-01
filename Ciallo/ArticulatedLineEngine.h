#pragma once

class ArticulatedLineEngine
{
	GLuint VertShader;
	GLuint GeomShader;
	GLuint FragShader;
	GLuint Program;
public:
	ArticulatedLineEngine() = default;
	void Init();
	void Destroy();
};

