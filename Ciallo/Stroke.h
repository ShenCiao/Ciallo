#pragma once

class Stroke
{
public:
	Geom::Polyline Position{};
	float Thickness = 0.0f; // This is half thickness (radius).
	std::vector<float> ThicknessOffset{}; // empty is allowed, values in shader are zero
	glm::vec4 Color = {0.0f, 0.0f, 0.0f, 1.0f};
	entt::entity BrushE = entt::null;
	int Type = 0;

	glm::vec4 FillColor = {0.0f, 0.0f, 0.0f, 1.0f};

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
	Stroke(Stroke&& other) noexcept;
	Stroke& operator=(const Stroke& other) = delete;
	Stroke& operator=(Stroke&& other) noexcept;
	~Stroke();

	void UpdateBuffers(int stampMode = 1); // I never expect I need to add this parameter
	void GenBuffers();
	void UpdatePositionBuffer();
	void UpdateThicknessOffsetBuffer();
	void UpdateDistanceBuffer(int stampMode = 1); 
	void LineDrawCall();
	void FillDrawCall();
	void SetUniforms();

private:
	void Zeroize();
};
