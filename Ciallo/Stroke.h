#pragma once

class Brush;

class Stroke : EntityObject
{
public:
	std::vector<Geom::Point> Position{};
	// This is supposed to be a Geom::Polyline class. But I'm not going to make extra unnecessary stuff for now.
	std::vector<float> Width{};
	Brush* Brush;

	/*
	 * Index: Attribute
	 * 0: position
	 * 1: width
	 */
	std::array<GLuint, 2> VertexBuffers = {0, 0};
	GLuint VertexArray = 0;

	Stroke();
	Stroke(const Stroke& other) = default;
	Stroke(Stroke&& other) noexcept = default;
	Stroke& operator=(const Stroke& other) = default;
	Stroke& operator=(Stroke&& other) noexcept = default;
	~Stroke();

	void GenBuffers();
	void UploadPositionData();
	void UploadWidthData();
};
