#include "pch.hpp"
#include "ArticulatedLineEngine.h"

#include "ShaderUtilities.h"

void ArticulatedLineEngine::Init()
{
	std::filesystem::path root = "./shaders";
	VertShader = ShaderUtilities::CreateFromFile(root/"articulatedLine.vert", GL_VERTEX_SHADER);
	GeomShader = ShaderUtilities::CreateFromFile(root/"articulatedLine.geom", GL_GEOMETRY_SHADER);
	FragShader = ShaderUtilities::CreateFromFile(root/"articulatedLine.frag", GL_FRAGMENT_SHADER);
	Program = glCreateProgram();
	glAttachShader(Program, VertShader);
	glAttachShader(Program, GeomShader);
	glAttachShader(Program, FragShader);
	glLinkProgram(Program);

	glDeleteShader(VertShader);
	glDeleteShader(GeomShader);
	glDeleteShader(FragShader);
}

void ArticulatedLineEngine::Destroy()
{
	glDeleteProgram(Program);
}
