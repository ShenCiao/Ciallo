#include "pch.hpp"
#include "ArticulatedLineEngine.h"

#include "ShaderUtilities.h"

ArticulatedLineEngine::ArticulatedLineEngine()
{
	std::filesystem::path root = "./shaders";
	GLuint vertShader = ShaderUtilities::CreateFromFile(root / "articulatedLine.vert", GL_VERTEX_SHADER);
	std::string geomCode = ShaderUtilities::LoadCodeFromFile(root / "articulatedLine.geom");
	GLuint geomShader = ShaderUtilities::CreateCompiled(geomCode, GL_GEOMETRY_SHADER);
	std::string fragCode = ShaderUtilities::LoadCodeFromFile(root / "articulatedLine.frag");
	
	GLuint vanilla = ShaderUtilities::CreateCompiled(fragCode, GL_FRAGMENT_SHADER);
	std::string stampCode = fragCode;
	stampCode.insert(13, "#define STAMP");
	GLuint stamp = ShaderUtilities::CreateCompiled(stampCode, GL_FRAGMENT_SHADER);
	std::string airbrushCode = fragCode;
	airbrushCode.insert(13, "#define AIRBRUSH");
	GLuint airbrush = ShaderUtilities::CreateCompiled(airbrushCode, GL_FRAGMENT_SHADER);

	GLuint program;
	program = glCreateProgram();
	glAttachShader(program, vertShader);
	glAttachShader(program, geomShader);
	glAttachShader(program, vanilla);
	glLinkProgram(program);
	Programs[Type::Vanilla] = program;

	program = glCreateProgram();
	glAttachShader(program, vertShader);
	glAttachShader(program, geomShader);
	glAttachShader(program, stamp);
	glLinkProgram(program);
	Programs[Type::Stamp] = program;

	program = glCreateProgram();
	glAttachShader(program, vertShader);
	glAttachShader(program, geomShader);
	glAttachShader(program, airbrush);
	glLinkProgram(program);
	Programs[Type::Airbrush] = program;
}

ArticulatedLineEngine::~ArticulatedLineEngine()
{
	for(auto& it : Programs)
	{
		glDeleteProgram(it.second);
	}
}

GLuint ArticulatedLineEngine::Program(Type type)
{
	return Programs[type];
}
