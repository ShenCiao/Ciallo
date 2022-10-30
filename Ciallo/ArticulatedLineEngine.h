#pragma once

class ArticulatedLineEngine
{
	GLuint mVertShader;
	GLuint mGeomShader;
	GLuint mFragShader;
	GLuint mProgram;
public:
	ArticulatedLineEngine() = default;
	void Init();
	void Destroy();
};

