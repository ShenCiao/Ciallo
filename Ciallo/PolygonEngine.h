#pragma once

#include "Polyline.h"

class PolygonEngine
{
public:
	GLuint VertShader;
	GLuint FragShader;
	GLuint Program;

	PolygonEngine();
	~PolygonEngine();

	static void DrawPolygon(std::vector<Geom::Polyline> polygonWithHoles);
};
