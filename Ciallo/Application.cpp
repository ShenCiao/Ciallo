#include "pch.hpp"
#include "Application.h"
#include "ArticulatedLineEngine.h"

#include <implot.h>

Application::Application()
{

}

void Application::Run()
{
	Window w;
	ArticulatedLineEngine engine;
	engine.Init();
	engine.Destroy();

	while(!w.ShouldClose())
	{
		w.BeginFrame();
		ImPlot::ShowDemoWindow();
		ImGui::ShowDemoWindow();
		w.EndFrame();
	}
}
