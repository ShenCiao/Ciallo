#pragma once
#ifndef ASTAMP_
#define ASTAMP_

#include <string>
#include <vector>
class AStamp
{
public:
	static inline std::vector<GLuint> Stamps;

	static void LoadStamp(std::vector<std::string> fileNames);
	static void SaveStamp(GLuint dsa2DTexture, std::string nameNoSuffix);
};

#endif
