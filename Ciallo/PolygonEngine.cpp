#include "pch.hpp"
#include "PolygonEngine.h"

#include "ShaderUtilities.h"

PolygonEngine::PolygonEngine()
{
	std::filesystem::path root = "./shaders";
	VertShader = ShaderUtilities::CreateFromFile(root / "polygon.vert", GL_VERTEX_SHADER);
	FragShader = ShaderUtilities::CreateFromFile(root / "polygon.frag", GL_FRAGMENT_SHADER);
	Program = glCreateProgram();
	glAttachShader(Program, VertShader);
	glAttachShader(Program, FragShader);
	glLinkProgram(Program);

	glDeleteShader(VertShader);
	glDeleteShader(FragShader);
}

PolygonEngine::~PolygonEngine()
{
	glDeleteProgram(Program);
}

void PolygonEngine::DrawPolygon(std::vector<Geom::Polyline> polygonWithHoles)
{
	if (polygonWithHoles.empty()) return;

	// glClear(GL_STENCIL_BUFFER_BIT);
	glColorMask(GL_FALSE, GL_FALSE, GL_FALSE, GL_FALSE);
	glStencilFunc(GL_ALWAYS, 0, 1);
	glStencilOp(GL_KEEP, GL_KEEP, GL_INVERT);
	glStencilMask(1);
	//-------------------
	GLuint vao = 0;
	glCreateVertexArrays(1, &vao);
	glEnableVertexArrayAttrib(vao, 0);
	glVertexArrayAttribBinding(vao, 0, 0);
	glVertexArrayAttribFormat(vao, 0, 2, GL_FLOAT, GL_FALSE, 0);
	glBindVertexArray(vao);

	std::vector<GLuint> vbos(polygonWithHoles.size());
	glCreateBuffers(vbos.size(), vbos.data());

	for (int i = 0; i < polygonWithHoles.size(); ++i)
	{
		auto& polygon = polygonWithHoles[i];
		GLuint vbo = vbos[i];

		glNamedBufferData(vbo, polygon.size() * sizeof(glm::vec2), polygon.data(), GL_DYNAMIC_DRAW);
		glVertexArrayVertexBuffer(vao, 0, vbo, 0, sizeof(glm::vec2));

		glDrawArrays(GL_TRIANGLE_FAN, 0, polygon.size());
		//-------------------
	}

	glColorMask(GL_TRUE, GL_TRUE, GL_TRUE, GL_TRUE);
	glStencilFunc(GL_EQUAL, 1, 1);
	glVertexArrayVertexBuffer(vao, 0, vbos[0], 0, sizeof(glm::vec2));
	glDrawArrays(GL_TRIANGLE_FAN, 0, polygonWithHoles[0].size());

	//-------------------
	glDeleteBuffers(vbos.size(), vbos.data());
	glDeleteVertexArrays(1, &vao);
}
