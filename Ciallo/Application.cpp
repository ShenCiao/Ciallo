#include "pch.hpp"
#include "Application.h"

#include "ArticulatedLineEngine.h"
#include "CanvasPanel.h"
#include "Drawing.h"
#include "Project.h"

#include <implot.h>

Application::Application()
{
	Window = std::make_unique<class Window>();
}

void Application::Run()
{
	GenDefaultProject();
	
	while (!Window->ShouldClose())
	{
		Window->BeginFrame();
		ActiveProject->CanvasPanel.Draw();
		ImPlot::ShowDemoWindow();
		ImGui::ShowDemoWindow();
		Window->EndFrame();
	}
}

void Application::GenDefaultProject()
{
	ActiveProject = std::make_unique<Project>();
	EntityObject::Registry = &ActiveProject->Registry;

	auto drawing = std::make_unique<Drawing>();
	drawing->UpperLeft = {0.0f, 0.0f};
	drawing->LowerRight = {0.297f, 0.21f};
	drawing->Dpi = 144.0f;
	drawing->GenRenderTarget();
	ActiveProject->MainDrawing = std::move(drawing);

	CanvasPanel canvas;
	canvas.ActiveDrawing = ActiveProject->MainDrawing.get();
	ActiveProject->CanvasPanel = std::move(canvas);
}
