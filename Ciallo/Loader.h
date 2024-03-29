#pragma once

#include <filesystem>

class Loader
{
public:
	static void LoadCsv(const std::filesystem::path& filePath, float targetThickness = 0.002f);
	static void SaveCsv(const std::filesystem::path& filePath);
};

