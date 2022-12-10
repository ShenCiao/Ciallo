#pragma once
class TextureManager
{
public:
	static inline std::vector<GLuint> Textures;

	static void LoadTextures();
	static void SaveTexture(GLuint dsa2DTexture, std::string nameNoSuffix);
};

