#include "pch.hpp"
#include "Window.h"

#define GLFW_EXPOSE_NATIVE_WIN32
#include <GLFW/glfw3native.h>
#include <implot.h>
#include <imgui_impl_glfw.h>
#include <imgui_impl_opengl3.h>
#define EASYTAB_IMPLEMENTATION
#include "easytab.h"

#include "EyedropperInfo.h"

Window::Window()
{
	glfwInit();
	glfwSetErrorCallback(GlfwErrorCallback);
	glfwWindowHint(GLFW_CLIENT_API, GLFW_OPENGL_API);
	glfwWindowHint(GLFW_RESIZABLE, GLFW_FALSE);
	glfwWindowHint(GLFW_OPENGL_PROFILE, GLFW_OPENGL_CORE_PROFILE);
	glfwWindowHint(GLFW_CONTEXT_VERSION_MAJOR, 4);
	glfwWindowHint(GLFW_CONTEXT_VERSION_MINOR, 6);
	glfwWindowHint(GLFW_OPENGL_DEBUG_CONTEXT, GLFW_FALSE);
	// GlfwWindow = glfwCreateWindow(1920, 1080, "Anonymous", glfwGetPrimaryMonitor(), nullptr); // Full screen mode
	//GlfwWindow = glfwCreateWindow(2560, 1440, "Anonymous", glfwGetPrimaryMonitor(), nullptr); // Full screen mode
	 GlfwWindow = glfwCreateWindow(3840, 2160, "Anonymous", glfwGetPrimaryMonitor(), nullptr); // Full screen mode
	//GlfwWindow = glfwCreateWindow(2560, 1440, "Anonymous", nullptr, nullptr); // Window mode
	if (!GlfwWindow)
	{
		throw std::runtime_error("Fail on init window");
	}
	glfwMakeContextCurrent(GlfwWindow);
	glfwSwapInterval(0);

	if (int version = gladLoadGL(); version == 0)
	{
		throw std::runtime_error("Fail on init glad");
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
	glEnable(GL_BLEND);

	ImGui::CreateContext();
	ImPlot::CreateContext();
	auto& io = ImGui::GetIO();
	io.ConfigFlags |= ImGuiConfigFlags_DockingEnable;
	io.ConfigWindowsMoveFromTitleBarOnly = true;
	io.FontGlobalScale = 1.5f;
	ImGui_ImplGlfw_InitForOpenGL(GlfwWindow, true);
	ImGui_ImplOpenGL3_Init("#version 460");

	// Get Windows pen pressure events
	EasyTab_Load(glfwGetWin32Window(GlfwWindow));
}

Window::~Window()
{
	ImGui_ImplOpenGL3_Shutdown();
	ImGui_ImplGlfw_Shutdown();
	ImPlot::DestroyContext();
	ImGui::DestroyContext();

	EasyTab_Unload();

	glfwDestroyWindow(GlfwWindow);
	glfwTerminate();
}

bool Window::ShouldClose() const
{
	return glfwWindowShouldClose(GlfwWindow);
}

void Window::BeginFrame() const
{
	glBindFramebuffer(GL_FRAMEBUFFER, 0);
	const ImVec4 clearColor{ 1.0f, 1.0f, 1.0f, 1.0f };
	glClear(GL_COLOR_BUFFER_BIT | GL_DEPTH_BUFFER_BIT | GL_STENCIL_BUFFER_BIT);
	glClearColor(clearColor.x, clearColor.y, clearColor.z, clearColor.w);
	MSG msg;
	float pressure = 0.0f;
	if (PeekMessageW(&msg, NULL, 0, 0, PM_NOREMOVE))
	{
		if (EasyTab_HandleEvent(msg.hwnd, msg.message, msg.lParam, msg.wParam) == EASYTAB_OK)
		{
			pressure = EasyTab->Pressure;
		}
		else pressure = ImGui::GetIO().PenPressure;
	}
	else pressure = ImGui::GetIO().PenPressure;

	glfwPollEvents();
	ImGui_ImplOpenGL3_NewFrame();
	ImGui_ImplGlfw_NewFrame();
	ImGui::NewFrame();
	
	ImGui::GetIO().PenPressure = pressure;
}

void Window::EndFrame() const
{	
	ImGui::Render();
	ImGui_ImplOpenGL3_RenderDrawData(ImGui::GetDrawData());

	auto& info = R.ctx().get<EyedropperInfo>();
	if (info.IsPicking) {
		int dims[] = { 0, 0, 0, 0 };
		glfwGetFramebufferSize(GlfwWindow, &dims[2], &dims[3]);
		int x = static_cast<int>(ImGui::GetMousePos().x);
		int y = static_cast<int>(dims[3] - ImGui::GetMousePos().y);
		glm::vec3 color;
		glReadPixels(x, y, 1, 1, GL_RGB, GL_FLOAT, &color);
		info.Color = color;
	}
	
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
		// spdlog::info("GL callback: Type = {}, Message = {}", errorType2String[type], message);
		break;
	default:
		break;
	}
}
