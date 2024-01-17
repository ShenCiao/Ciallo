#pragma once

#include "Window.h"

class Application
{
public:
	std::unique_ptr<Window> Window; //?

	Application();

	void Run();
	void GenDefaultProject();
};
