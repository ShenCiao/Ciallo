#include "pch.hpp"
#include "Application.h"

#include <implot.h>

Application::Application()
{

}

void Application::Run()
{
	Window w;
	while(!w.ShouldClose())
	{
		w.BeginFrame();
		ImPlot::ShowDemoWindow();
		ImGui::ShowDemoWindow();
		w.EndFrame();
	}
}
