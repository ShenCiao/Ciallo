#pragma once

class PolygonEngine
{
public:
	GLuint VertShader;
	GLuint FragShader;
	GLuint Program;

	PolygonEngine();
	~PolygonEngine();
};
