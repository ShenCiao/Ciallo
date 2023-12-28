#pragma once

#include "RenderableTexture.h"
#include "AirBrushData.h"
#include "StampBrushData.h"

class Brush
{
public:
	// Program identifier is owned by other;
	GLuint Program = 0;
	std::string Name;
	RenderableTexture PreviewTexture{};

	std::unique_ptr<AirBrushData> AirBrush;
	std::unique_ptr<StampBrushData> Stamp;

	Brush() = default;
	Brush(const Brush& other);
	Brush(Brush&& other) noexcept;
	Brush& operator=(Brush other);
	friend void swap(Brush& lhs, Brush& rhs) noexcept;

	void SetUniforms() const;
	void Use() const;
	void DrawProperties();

	template<class Archive>
	void serialize(Archive& archive) {
		archive(Program, Name);
	}
};
