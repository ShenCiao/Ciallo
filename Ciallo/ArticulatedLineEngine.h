#pragma once
#include "Stroke.h"

class ArticulatedLineEngine
{
public:
	GLuint VertShader;
	GLuint GeomShader;
	GLuint FragShader;
	GLuint Program;

	ArticulatedLineEngine();
	~ArticulatedLineEngine();

	void DrawStroke(Stroke* stroke) const;
};
