﻿#pragma once

// RAII object
class ColorFace
{
public:
	std::vector<Geom::Polyline> PolygonWithHoles;// a polygon at element 0 and holes are the rest.
	glm::vec4 Color;

	std::vector<GLuint> VertexBuffers; 
	GLuint VertexArray = 0;

	ColorFace(std::vector<Geom::Polyline> polygonWithHoles);
	ColorFace(const ColorFace& other) = delete;
	ColorFace(ColorFace&& other) noexcept;
	ColorFace& operator=(const ColorFace& other) = delete;
	ColorFace& operator=(ColorFace&& other) noexcept;
	~ColorFace();

	void FillDrawCall();
	void LineDrawCall();
	void GenUploadBuffers();
	void DeleteBuffers();
};
