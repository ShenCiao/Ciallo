#pragma once

class StampBrushData
{
public:
	GLuint StampTexture = 0;
	float StampIntervalRatio = 0;

	void SetUniform();
};

