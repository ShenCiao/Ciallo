#pragma once

class StampBrushData
{
public:
	GLuint StampTexture = 0;
	float StampIntervalRatio = 0.0f;
	float NoiseFactor = 0.0f;
	float RotationRand = 1.0f;

	enum StampMode { EquiDistant, RatioDistant };
	StampMode StampMode = RatioDistant;

	StampBrushData() = default;

	void SetUniforms();
	void DrawProperties();

	template<class Archive>
	void serialize(Archive& archive) {
		archive(StampTexture, StampIntervalRatio, NoiseFactor, RotationRand);
	}
};

