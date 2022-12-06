#include "pch.hpp"
#include "ShaderUtilities.h"

#include <fstream>

GLuint ShaderUtilities::CreateFromFile(const std::filesystem::path& filePath, GLenum type, const std::string& macro)
{
	std::string code = LoadCodeFromFile(filePath);
	code.insert(13, macro); // insert after version 460
	return CreateCompiled(code, type);
}

std::string ShaderUtilities::LoadCodeFromFile(const std::filesystem::path& filePath)
{
	std::ifstream shaderFile;
	shaderFile.open(filePath);
	shaderFile.exceptions(std::ifstream::failbit | std::ifstream::badbit);
	std::stringstream shaderStream;
	shaderStream << shaderFile.rdbuf();
	std::string code = shaderStream.str();
	shaderFile.close();
	return code;
}

GLuint ShaderUtilities::CreateCompiled(const std::string& code, GLenum type)
{
	GLuint shader = glCreateShader(type);
	auto c = code.c_str();
	glShaderSource(shader, 1, &c, nullptr);
	glCompileShader(shader);

	int success;
	glGetShaderiv(shader, GL_COMPILE_STATUS, &success);
	if (!success)
	{
		char infoLog[512];
		glGetShaderInfoLog(shader, 512, nullptr, infoLog);
		spdlog::error("{} shader fails on compile:", ShaderType2String(type), infoLog);
	}

	return shader;
}

std::string ShaderUtilities::ShaderType2String(GLenum type)
{
	std::unordered_map<GLenum, std::string> map{
		{GL_VERTEX_SHADER, "Vertex"},
		{GL_TESS_CONTROL_SHADER, "TessControl"},
		{GL_TESS_EVALUATION_SHADER, "TessEval"},
		{GL_GEOMETRY_SHADER, "Geometry"},
		{GL_FRAGMENT_SHADER, "Fragment"},
		{GL_COMPUTE_SHADER, "Compute"},
	};

	return map[type];
}
