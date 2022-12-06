#include "pch.hpp"
#include "TextureManager.h"

#define STB_IMAGE_IMPLEMENTATION
#include <stb_image.h>
#include <filesystem>

void TextureManager::LoadTextures()
{
	stbi_set_flip_vertically_on_load(true);
	std::vector<std::string> fileNames{ "stamp1.png" };
	std::filesystem::path root{ "./images" };
	for(auto& file: fileNames)
	{
		int width, height, channel;
		unsigned char* data = stbi_load((root/file).string().c_str(), &width, &height, &channel, 4);
		if (!data) throw std::runtime_error("cannot load images");
		GLuint tex;
		glCreateTextures(GL_TEXTURE_2D, 1, &tex);
		glTextureStorage2D(tex, 1, GL_RGBA8, width, height);
		glTextureSubImage2D(tex, 0, 0, 0, width, height, GL_RGBA, GL_UNSIGNED_BYTE, data);
		glTextureParameteri(tex, GL_TEXTURE_MIN_FILTER, GL_NEAREST);
		glTextureParameteri(tex, GL_TEXTURE_MAG_FILTER, GL_NEAREST);
		glTextureParameteri(tex, GL_TEXTURE_WRAP_S, GL_CLAMP_TO_EDGE);
		glTextureParameteri(tex, GL_TEXTURE_WRAP_T, GL_CLAMP_TO_EDGE);
		Textures.push_back(tex);
	}
}
