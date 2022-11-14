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
