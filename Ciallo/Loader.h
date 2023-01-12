#pragma once

#include <filesystem>

class Loader
{
public:
	static void LoadCsv(const std::filesystem::path& filePath);
};

