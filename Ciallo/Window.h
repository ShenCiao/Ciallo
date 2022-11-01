#pragma once

#include <GLFW/glfw3.h>

class Window
{
	GLFWwindow* GlfwWindow = nullptr;

public:
	Window();
	~Window();

	bool ShouldClose() const;
	void BeginFrame() const;
	void EndFrame() const;
private:
	static void GlfwErrorCallback(int error, const char* description);
	static void GlDebugCallback(GLenum source, GLenum type, GLuint id, GLenum severity, GLsizei length,
	                              const GLchar* message, const void* userParam);
};
