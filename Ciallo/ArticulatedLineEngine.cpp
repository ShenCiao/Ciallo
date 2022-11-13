#include "pch.hpp"
#include "ArticulatedLineEngine.h"

#include "ShaderUtilities.h"

ArticulatedLineEngine::ArticulatedLineEngine()
{
	std::filesystem::path root = "./shaders";
	VertShader = ShaderUtilities::CreateFromFile(root / "articulatedLine.vert", GL_VERTEX_SHADER);
	GeomShader = ShaderUtilities::CreateFromFile(root / "articulatedLine.geom", GL_GEOMETRY_SHADER);
	FragShader = ShaderUtilities::CreateFromFile(root / "articulatedLine.frag", GL_FRAGMENT_SHADER);
	Program = glCreateProgram();
	glAttachShader(Program, VertShader);
	glAttachShader(Program, GeomShader);
	glAttachShader(Program, FragShader);
	glLinkProgram(Program);

	glDeleteShader(VertShader);
	glDeleteShader(GeomShader);
	glDeleteShader(FragShader);
}

ArticulatedLineEngine::~ArticulatedLineEngine()
{
	glDeleteProgram(Program);
}

void ArticulatedLineEngine::DrawStroke(Stroke* stroke)
{
	int count = stroke->Position.size();
	if (count == 1)
	{
		Geom::Point p = stroke->Position[0];
		float w = stroke->Width[0];
		Geom::Point paddedPos = { p.x() + 0.01f * w, p.y() };

		stroke->Position.push_back(paddedPos);
		stroke->Width.push_back(w);
		stroke->UploadPositionData();
		stroke->UploadWidthData();

		stroke->Position.pop_back();
		stroke->Width.pop_back();

		count += 1;
	}
	glBindVertexArray(stroke->VertexArray);
	glDrawArrays(GL_LINE_STRIP, 0, count);
}
