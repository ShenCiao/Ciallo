#pragma once

class Stroke
{
public:
	Geom::Polyline Position{};
	float Thickness = 0.0f;
	std::vector<float> ThicknessOffset{}; // empty is allowed, values in shader are zero
	glm::vec4 Color = {0.0f, 0.0f, 0.0f, 1.0f};
	entt::entity Brush = entt::null;

	// Bad design but works!
	glm::vec4 PolygonColor = {0.0f, 0.0f, 0.0f, 1.0f/3.0f};

	/*
	 * Index----Attribute
	 * 0:	position
	 * 1:	thickness offset
	 * 2:	distance to the first vertex(prefix sum result)
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
	void UpdateThicknessOffsetBuffer();
	void UpdateDistanceBuffer();
	void DrawCall();
	void SetUniform();
};
