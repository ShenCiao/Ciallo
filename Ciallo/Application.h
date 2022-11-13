#pragma once

#include "Project.h"
#include "Window.h"
#include "RenderingSystem.h"

class Application
{
	std::unique_ptr<Project> ActiveProject;
	std::unique_ptr<Window> Window;
	inline static std::unique_ptr<RenderingSystem> RenderingSystem;
public:
	Application();

	void Run();
	void GenDefaultProject();
};
