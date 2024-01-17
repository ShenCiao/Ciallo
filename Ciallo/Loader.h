#pragma once

#include <filesystem>

class Loader
{
	inline static float TargetThickness = 0.002f;
	inline static int BrushIndex = 0;
	inline static float TargetAlpha = 1.0f;
	inline static float PressureThreshold = 0.8f;
	inline static float OrderThreshold = 0.6f;

	inline static int ContourIndex = 37;
	inline static int OccludedIndex = 65;
	inline static int HatchingIndex = 42;
	inline static int HatchingOutlineIndex = 72;
	inline static int DetailIndex = 56;

	inline static float ContourThickness = 0.0015f;
	inline static float OccludedThickness = 0.001f;
	inline static float HatchingThickness = 0.001f;
	inline static float HatchingOutlineThickness = 0.0012f;
	inline static float DetailThickness = 0.001f;

	inline static int PointsThreshold = 10;
	
	inline static glm::vec4 ContourColor = {0.0f, 0.0f, 0.0f, 1.0f};
	inline static glm::vec4 OccludedColor = {0.0f, 0.5f, 0.0f, 1.0f};
	inline static glm::vec4 HatchingColor = {0.5f, 0.0f, 0.0f, 0.8f};
	inline static glm::vec4 HatchingOutlineColor = {1.0f, 0.0f, 0.0f, 1.0f};
	inline static glm::vec4 DetailColor = {0.25f, 0.25f, 0.0f, 1.0f};

public:
	static void LoadCsv(const std::filesystem::path& filePath, int brushNum = 0, float rotateRandom = 1.0, float intervalRatio = 0.02, float noiseFactor = 0.01, float targetThickness = 0.001f); //, int brush = 0, int iterate = 0
	static void SaveCsv(const std::filesystem::path& filePath);
	static void DrawUI();
};

