#pragma once

class Brush;

class Stroke
{
public:
	std::vector<glm::vec2> Position{};
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

	Geom::Curve_handle CurveHandle;

	Stroke();
	Stroke(const Stroke& other) = delete;
	Stroke(Stroke&& other) = delete;
	Stroke& operator=(const Stroke& other) = delete;
	Stroke& operator=(Stroke&& other) noexcept = delete;
	~Stroke();

	void OnChanged();
	void GenBuffers();
	void UpdatePositionBuffer();
	void UpdateWidthBuffer();
};
