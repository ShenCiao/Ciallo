#include "pch.hpp"
#include "DotEngine.h"

#include "ShaderUtilities.h"

DotEngine::DotEngine()
{
	std::filesystem::path root = "./shaders";
	GLuint vertShader = ShaderUtilities::CreateFromFile(root / "dot.vert", GL_VERTEX_SHADER);
	GLuint geomShader = ShaderUtilities::CreateFromFile(root / "dot.geom", GL_GEOMETRY_SHADER);
	GLuint fragShader = ShaderUtilities::CreateFromFile(root / "dot.frag", GL_FRAGMENT_SHADER);
	Program = glCreateProgram();
	glAttachShader(Program, vertShader);
	glAttachShader(Program, geomShader);
	glAttachShader(Program, fragShader);
	glLinkProgram(Program);

	glDeleteShader(vertShader);
	glDeleteShader(fragShader);
}

DotEngine::~DotEngine()
{
	glDeleteProgram(Program);
}
