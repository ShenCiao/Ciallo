#include "pch.hpp"
#include "AStamp.h"


#include <stb_image.h>
#include <stb_image_write.h>

#include <filesystem>

void AStamp::LoadStamp(std::vector<std::string> fileNames)
{
	auto setStampParameter = [](GLuint tex)
	{
		glTextureParameteri(tex, GL_TEXTURE_MIN_FILTER, GL_NEAREST);
		glTextureParameteri(tex, GL_TEXTURE_MAG_FILTER, GL_NEAREST);
		glTextureParameteri(tex, GL_TEXTURE_WRAP_S, GL_CLAMP_TO_EDGE);
		glTextureParameteri(tex, GL_TEXTURE_WRAP_T, GL_CLAMP_TO_EDGE);
	};

	// Create an empty blank white texture
	GLuint ColorStamp;
	GLuint buffer;
	glCreateTextures(GL_TEXTURE_2D, 1, &ColorStamp);
	setStampParameter(ColorStamp);
	glTextureStorage2D(ColorStamp, 1, GL_RGBA8, 256, 256);
	glCreateFramebuffers(1, &buffer);												//Framebuffer?
	glNamedFramebufferTexture(buffer, GL_COLOR_ATTACHMENT0, ColorStamp, 0);
	glBindFramebuffer(GL_FRAMEBUFFER, buffer);
	glClearColor(1, 1, 1, 1);
	glClear(GL_COLOR_BUFFER_BIT);
	glBindFramebuffer(GL_FRAMEBUFFER, 0);
	if (glCheckNamedFramebufferStatus(buffer, GL_FRAMEBUFFER) != GL_FRAMEBUFFER_COMPLETE)
	{
		throw std::runtime_error("Framebuffer incomplete!");
	}
	Stamps.push_back(ColorStamp);

	stbi_set_flip_vertically_on_load(true);
	std::filesystem::path root{ "./stamps" };
	for (auto& file : fileNames)
	{
		int width, height, channel;
		unsigned char* data = stbi_load((root / file).string().c_str(), &width, &height, &channel, 4); // extract features from stamp image
		if (!data) throw std::runtime_error("cannot load images");
		GLuint tex;
		glCreateTextures(GL_TEXTURE_2D, 1, &tex);													// new texture object 
		glTextureStorage2D(tex, 1, GL_RGBA8, width, height);
		glTextureSubImage2D(tex, 0, 0, 0, width, height, GL_RGBA, GL_UNSIGNED_BYTE, data);
		glTextureParameteri(tex, GL_TEXTURE_MIN_FILTER, GL_NEAREST);
		glTextureParameteri(tex, GL_TEXTURE_MAG_FILTER, GL_NEAREST);
		glTextureParameteri(tex, GL_TEXTURE_WRAP_S, GL_CLAMP_TO_EDGE);
		glTextureParameteri(tex, GL_TEXTURE_WRAP_T, GL_CLAMP_TO_EDGE);
		Stamps.push_back(tex);
		//std::cout << tex << std::endl;
	}
}

void AStamp::SaveStamp(GLuint dsa2DTexture, std::string name)
{
	int width, height;
	glGetTextureLevelParameteriv(dsa2DTexture, 0, GL_TEXTURE_WIDTH, &width);
	glGetTextureLevelParameteriv(dsa2DTexture, 0, GL_TEXTURE_HEIGHT, &height);
	std::vector<char> imageData(width * height * 4);
	glGetTextureImage(dsa2DTexture, 0, GL_RGBA, GL_UNSIGNED_BYTE, imageData.size(), imageData.data());
	std::filesystem::path root{ "./images/rendered" };
	name.append(".png");
	stbi_write_png((root / name).string().c_str(), width, height, 4, imageData.data(), 0);
}
