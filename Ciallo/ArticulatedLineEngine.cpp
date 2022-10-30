#include "pch.hpp"
#include "ArticulatedLineEngine.h"

#include "ShaderUtilities.h"

void ArticulatedLineEngine::Init()
{
	std::filesystem::path root = "./shaders";
	mVertShader = ShaderUtilities::CreateFromFile(root/"articulatedLine.vert", GL_VERTEX_SHADER);
	mGeomShader = ShaderUtilities::CreateFromFile(root/"articulatedLine.geom", GL_GEOMETRY_SHADER);
	mFragShader = ShaderUtilities::CreateFromFile(root/"articulatedLine.frag", GL_FRAGMENT_SHADER);
	mProgram = glCreateProgram();
	glAttachShader(mProgram, mVertShader);
	glAttachShader(mProgram, mGeomShader);
	glAttachShader(mProgram, mFragShader);
	glLinkProgram(mProgram);

	glDeleteShader(mVertShader);
	glDeleteShader(mGeomShader);
	glDeleteShader(mFragShader);
}

void ArticulatedLineEngine::Destroy()
{
	glDeleteProgram(mProgram);
}
