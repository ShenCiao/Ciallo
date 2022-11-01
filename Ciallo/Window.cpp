#include "pch.hpp"
#include "Window.h"

#include <implot.h>
#include <imgui_impl_glfw.h>
#include <imgui_impl_opengl3.h>

Window::Window()
{
	glfwInit();
	glfwSetErrorCallback(GlfwErrorCallback);
	glfwWindowHint(GLFW_CLIENT_API, GLFW_OPENGL_API);
	glfwWindowHint(GLFW_RESIZABLE, GLFW_TRUE);
	glfwWindowHint(GLFW_MAXIMIZED, GLFW_TRUE);
	glfwWindowHint(GLFW_OPENGL_DEBUG_CONTEXT, true);
	glfwWindowHint(GLFW_CONTEXT_VERSION_MAJOR, 4);
	glfwWindowHint(GLFW_CONTEXT_VERSION_MINOR, 6);
	GlfwWindow = glfwCreateWindow(1000, 1000, "Ciallo Lab Version", nullptr, nullptr);
	if (!GlfwWindow)
	{
		throw std::runtime_error("Fail on init window");
	}
	glfwMakeContextCurrent(GlfwWindow);
	glfwSwapInterval(0); // TODO: default vsync introduce extra input-to-visual lag. Control it manually.

	if (GLenum err = glewInit())
	{
		throw std::runtime_error("Fail on init glew");
	}
	int contextFlags = 0;
	glGetIntegerv(GL_CONTEXT_FLAGS, &contextFlags);
	if (contextFlags & GL_CONTEXT_FLAG_DEBUG_BIT)
	{
		spdlog::info("GL: Debug context created!");
		glEnable(GL_DEBUG_OUTPUT);
		glDebugMessageCallback(GlDebugCallback, nullptr);
		glDebugMessageControl(GL_DONT_CARE, GL_DONT_CARE, GL_DONT_CARE, 0, nullptr, GL_TRUE);
	}

	ImGui::CreateContext();
	ImPlot::CreateContext();
	auto& io = ImGui::GetIO();
	io.ConfigFlags |= ImGuiConfigFlags_DockingEnable;
	io.ConfigWindowsMoveFromTitleBarOnly = true;
	io.FontGlobalScale = 1.5f;
	ImGui_ImplGlfw_InitForOpenGL(GlfwWindow, true);
	ImGui_ImplOpenGL3_Init("#version 460");
}

Window::~Window()
{
	ImGui_ImplOpenGL3_Shutdown();
	ImGui_ImplGlfw_Shutdown();
	ImPlot::DestroyContext();
	ImGui::DestroyContext();

	glfwDestroyWindow(GlfwWindow);
	glfwTerminate();
}

bool Window::ShouldClose() const
{
	return glfwWindowShouldClose(GlfwWindow);
}

void Window::BeginFrame() const
{
	glfwPollEvents();
	ImGui_ImplOpenGL3_NewFrame();
	ImGui_ImplGlfw_NewFrame();
	ImGui::NewFrame();
	ImGui::DockSpaceOverViewport();
}

void Window::EndFrame() const
{
	const ImVec4 clearColor{ .0f, .0f, 0.0f, 1.00f };
	glClear(GL_COLOR_BUFFER_BIT | GL_DEPTH_BUFFER_BIT);
	glClearColor(clearColor.x, clearColor.y, clearColor.z, clearColor.w);
	glBindFramebuffer(GL_FRAMEBUFFER, 0);
	ImGui::Render();
	ImGui_ImplOpenGL3_RenderDrawData(ImGui::GetDrawData());
	glfwSwapBuffers(GlfwWindow);
}

void Window::GlfwErrorCallback(int error, const char* description)
{
	spdlog::error("Glfw error: {}", description);
}

void Window::GlDebugCallback(GLenum source, GLenum type, GLuint id, GLenum severity, GLsizei length,
                             const GLchar* message, const void* userParam)
{
	std::unordered_map<GLenum, std::string> errorType2String{
		{GL_DEBUG_TYPE_ERROR, "ERROR"},
		{GL_DEBUG_TYPE_DEPRECATED_BEHAVIOR, "DEPRECATED_BEHAVIOR"},
		{GL_DEBUG_TYPE_UNDEFINED_BEHAVIOR, "UNDEFINED_BEHAVIOR"},
		{GL_DEBUG_TYPE_PORTABILITY, "PORTABILITY"},
		{GL_DEBUG_TYPE_PERFORMANCE, "PERFORMANCE"},
		{GL_DEBUG_TYPE_MARKER, "MARKER"},
		{GL_DEBUG_TYPE_PUSH_GROUP, "PUSH_GROUP"},
		{GL_DEBUG_TYPE_POP_GROUP, "POP_GROUP"},
		{GL_DEBUG_TYPE_OTHER, "OTHER"},
	};
	switch (severity)
	{
	case GL_DEBUG_SEVERITY_HIGH:
		spdlog::error("GL callback: Type = {}, Message = {}", errorType2String[type], message);
		break;
	case GL_DEBUG_SEVERITY_MEDIUM:
		spdlog::warn("GL callback: Type = {}, Message = {}", errorType2String[type], message);
		break;
	case GL_DEBUG_SEVERITY_LOW:
	case GL_DEBUG_SEVERITY_NOTIFICATION:
		spdlog::info("GL callback: Type = {}, Message = {}", errorType2String[type], message);
		break;
	default:
		break;
	}
}
