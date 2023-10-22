#include "pch.hpp"
#include "ArticulatedLineComp.h"

#include "ShaderUtilities.h"

ArticulatedLineComp::ArticulatedLineComp()
{
	std::filesystem::path root = "./shaders";
	GLuint compShader = ShaderUtilities::CreateFromFile(root / "articulatedLine.comp", GL_COMPUTE_SHADER);

	Program = glCreateProgram();
	glAttachShader(Program, compShader);
	glLinkProgram(Program);

	glDeleteShader(compShader);
}

ArticulatedLineComp::~ArticulatedLineComp()
{
	glDeleteProgram(Program);
}
