#pragma once

class ArticulatedLineEngine
{
public:
	GLuint VertShader;
	GLuint GeomShader;
	GLuint FragShader;
	GLuint Program;

	ArticulatedLineEngine();
	~ArticulatedLineEngine();
};

