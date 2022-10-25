#pragma once

enum class BrushType
{
	Unknown,
	ArticulatedLine,
	EquidistantDot,
};

class ArticulatedLineBrushData;
class EquidistantDotBrushData;

// TODO: Polyline generation's parameter and algorithms should be implemented with brush.
class Brush
{
	BrushType mType = BrushType::Unknown;
	glm::vec4 mColor{.0f, .0f, .0f, 1.0f};
	std::unique_ptr<EquidistantDotBrushData> mEquidistantDotData = nullptr;
	std::unique_ptr<ArticulatedLineBrushData> mArticulatedLineData = nullptr;

public:
	Brush() = default;
};

