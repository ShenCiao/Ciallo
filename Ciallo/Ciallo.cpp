#include "pch.hpp"
#include "Application.h"

extern "C" {

__declspec(dllexport) DWORD NvOptimusEnablement = 1;
__declspec(dllexport) int AmdPowerXpressRequestHighPerformance = 1;

}

int main()
{
	Application app;

	app.Run();

	return 0;
}
