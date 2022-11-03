#include "pch.hpp"
#include "Application.h"

#include "ArticulatedLineEngine.h"
#include "CanvasPanel.h"
#include "Drawing.h"
#include "Project.h"

#include <implot.h>

Application::Application()
{

}

void Application::Run()
{
	Window w;

	Drawing d;
	d.Dpi = 72.0f;
	d.LowerRight = { 1.0f, 1.0f };
	d.GenRenderTarget();

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

Project Application::CreateDefaultProject()
{
	Project p;
	auto& r = p.Registry;
	entt::entity drawingE = r.create();
	auto& drawing = r.emplace<Drawing>(drawingE);
	// A4 paper setup.
	drawing.UpperLeft = { 0.f, 0.f };
	drawing.LowerRight = { 0.297f, 0.21f };
	drawing.Dpi = 72.f;
	drawing.GenRenderTarget();

	return p;
}
