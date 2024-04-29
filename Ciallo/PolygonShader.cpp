#include "pch.hpp"
#include "PolygonShader.h"

#include "ShaderUtilities.h"

PolygonShader::PolygonShader()
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

PolygonShader::~PolygonShader()
{
	glDeleteProgram(Program);
}
