#include "pch.hpp"
#include "Application.h"

#include "ArticulatedLineEngine.h"
#include "CanvasPanel.h"

#include <implot.h>

Application::Application()
{

}

void Application::Run()
{
	Window w;
	ArticulatedLineEngine engine{};
	engine.Init();
	engine.Destroy();

	CanvasPanel cp;
	cp.LoadTestImage();

	while(!w.ShouldClose())
	{
		w.BeginFrame();
		cp.Draw();
		ImPlot::ShowDemoWindow();
		ImGui::ShowDemoWindow();
		w.EndFrame();
	}
}
