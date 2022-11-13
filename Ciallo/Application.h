#pragma once

#include "Project.h"
#include "Window.h"

class Application
{
public:
	std::unique_ptr<Project> ActiveProject;
	std::unique_ptr<Window> Window;

	Application();

	void Run();
	void GenDefaultProject();
};
