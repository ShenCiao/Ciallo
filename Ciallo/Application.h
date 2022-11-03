#pragma once

#include "Project.h"
#include "Window.h"

class Application
{
public:
	Application();

	void Run();
	Project CreateDefaultProject();
};
