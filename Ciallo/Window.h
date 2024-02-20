#pragma once

#include <GLFW/glfw3.h>

class Window
{
public:
	GLFWwindow* GlfwWindow = nullptr;

	Window();
	~Window();

	bool ShouldClose() const;
	void BeginFrame() const;
	void EndFrame() const;
	bool IsMinimized() const;
private:
	static void GlfwErrorCallback(int error, const char* description);
	static void GlDebugCallback(GLenum source, GLenum type, GLuint id, GLenum severity, GLsizei length,
	                              const GLchar* message, const void* userParam);
	static void WindowIconifyCallback(GLFWwindow* window, int iconified)
	{
		if (iconified)
		{
			while(glfwGetWindowAttrib(window, GLFW_ICONIFIED)){
				glfwWaitEvents();
			}
		}
	}
};
