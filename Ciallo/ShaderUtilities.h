#pragma once

#include <filesystem>

struct ShaderUtilities
{
	static GLuint CreateFromFile(const std::filesystem::path& filePath, GLenum type);
	static std::string LoadCodeFromFile(const std::filesystem::path& filePath);
	static GLuint CreateCompiled(const std::string& code, GLuint type);
	static std::string ShaderType2String(GLenum type);
};
