#pragma once

#include <filesystem>
#include <cereal/archives/binary.hpp>
#include <cereal/types/vector.hpp>
#include <cereal/types/string.hpp>
#include <cereal/types/memory.hpp>

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
<<<<<<< HEAD
	static void LoadCsv(const std::filesystem::path& filePath, int brushNum = 0, float rotateRandom = 1.0, float intervalRatio = 0.02, float noiseFactor = 0.01, float targetThickness = 0.001f); //, int brush = 0, int iterate = 0
	static void SaveCsv(const std::filesystem::path& filePath);
	static void DrawUI();
=======
	static inline bool ShouldLoadProject = false;
	static void LoadCsv(const std::filesystem::path& filePath, float targetRadius = 0.002f);
	static void SaveCsv(const std::filesystem::path& filePath);
	static void LoadAnimation(const std::filesystem::path& filePath, float targetRadius = 0.001f);
	static void LoadProject(const std::filesystem::path& filePath);
	static void SaveProject(const std::filesystem::path& filePath);
>>>>>>> 42024b9a0433da43e6391d7b351638c29507b4bf
};

namespace glm
{

	template<class Archive> void serialize(Archive& archive, glm::vec2& v) { archive(v.x, v.y); }
	template<class Archive> void serialize(Archive& archive, glm::vec3& v) { archive(v.x, v.y, v.z); }
	template<class Archive> void serialize(Archive& archive, glm::vec4& v) { archive(v.x, v.y, v.z, v.w); }
	template<class Archive> void serialize(Archive& archive, glm::ivec2& v) { archive(v.x, v.y); }
	template<class Archive> void serialize(Archive& archive, glm::ivec3& v) { archive(v.x, v.y, v.z); }
	template<class Archive> void serialize(Archive& archive, glm::ivec4& v) { archive(v.x, v.y, v.z, v.w); }
	template<class Archive> void serialize(Archive& archive, glm::uvec2& v) { archive(v.x, v.y); }
	template<class Archive> void serialize(Archive& archive, glm::uvec3& v) { archive(v.x, v.y, v.z); }
	template<class Archive> void serialize(Archive& archive, glm::uvec4& v) { archive(v.x, v.y, v.z, v.w); }
	template<class Archive> void serialize(Archive& archive, glm::dvec2& v) { archive(v.x, v.y); }
	template<class Archive> void serialize(Archive& archive, glm::dvec3& v) { archive(v.x, v.y, v.z); }
	template<class Archive> void serialize(Archive& archive, glm::dvec4& v) { archive(v.x, v.y, v.z, v.w); }

	// glm matrices serialization
	template<class Archive> void serialize(Archive& archive, glm::mat2& m) { archive(m[0], m[1]); }
	template<class Archive> void serialize(Archive& archive, glm::dmat2& m) { archive(m[0], m[1]); }
	template<class Archive> void serialize(Archive& archive, glm::mat3& m) { archive(m[0], m[1], m[2]); }
	template<class Archive> void serialize(Archive& archive, glm::mat4& m) { archive(m[0], m[1], m[2], m[3]); }
	template<class Archive> void serialize(Archive& archive, glm::dmat4& m) { archive(m[0], m[1], m[2], m[3]); }

	template<class Archive> void serialize(Archive& archive, glm::quat& q) { archive(q.x, q.y, q.z, q.w); }
	template<class Archive> void serialize(Archive& archive, glm::dquat& q) { archive(q.x, q.y, q.z, q.w); }

}
