#include "pch.hpp"
#include "PolygonEngine.h"

#include "ShaderUtilities.h"

PolygonEngine::PolygonEngine()
{
	std::filesystem::path root = "./shaders";
	GLuint vertShader = ShaderUtilities::CreateFromFile(root / "polygon.vert", GL_VERTEX_SHADER);
	GLuint fragShader = ShaderUtilities::CreateFromFile(root / "polygon.frag", GL_FRAGMENT_SHADER);
	Program = glCreateProgram();
	glAttachShader(Program, vertShader);
	glAttachShader(Program, fragShader);
	glLinkProgram(Program);

	glDeleteShader(vertShader);
	glDeleteShader(fragShader);
}

PolygonEngine::~PolygonEngine()
{
	glDeleteProgram(Program);
}
