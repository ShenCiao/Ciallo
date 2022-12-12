#include "pch.hpp"
#include "PrefixSumPosition.h"

#include "ShaderUtilities.h"

PrefixSumPosition::PrefixSumPosition()
{
	std::filesystem::path root = "./shaders";
	GLuint compShader = ShaderUtilities::CreateFromFile(root / "prefixSumPosition.comp", GL_COMPUTE_SHADER);

	Program = glCreateProgram();
	glAttachShader(Program, compShader);
	glLinkProgram(Program);

	glDeleteShader(compShader);
}

PrefixSumPosition::~PrefixSumPosition()
{
	glDeleteProgram(Program);
}
