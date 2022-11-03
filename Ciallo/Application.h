#pragma once

#include "Project.h"
#include "Window.h"

class Application
{
	std::unique_ptr<Project> ActiveProject;
	std::unique_ptr<Window> Window;
public:
	Application();

	void Run();
	void GenDefaultProject();
};
