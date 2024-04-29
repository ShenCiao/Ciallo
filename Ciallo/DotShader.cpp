#include "pch.hpp"
#include "DotShader.h"

#include "ShaderUtilities.h"

DotShader::DotShader()
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
	glDeleteShader(geomShader);
	glDeleteShader(fragShader);
}

DotShader::~DotShader()
{
	glDeleteProgram(Program);
}
