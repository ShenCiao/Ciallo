#include "pch.hpp"
#include "Application.h"

extern "C" {
	_declspec(dllexport) DWORD NvOptimusEnablement = 1;
	_declspec(dllexport) int AmdPowerXpressRequestHighPerformance = 1;
}


int main()
{
	Application app;
	
	app.Run();
}