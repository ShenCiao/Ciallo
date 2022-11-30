#pragma once

class Brush;

class Stroke
{
public:
	Geom::Polyline Position{};
	std::vector<float> Width{};
	Brush* Brush;

	// Bad design but works!
	glm::vec4 PolygonColor = {0.0f, 0.0f, 0.0f, 1.0f/3.0f};
	/*
	 * Index: Attribute
	 * 0: position
	 * 1: width
	 * 2: distance to the first vertex(prefix sum result)
	 */
	std::array<GLuint, 3> VertexBuffers{};
	GLuint VertexArray = 0;

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
	void UpdateDistanceBuffer();
	void Draw();
};
