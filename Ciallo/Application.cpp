#include "pch.hpp"
#include "Application.h"

#include "CanvasPanel.h"
#include "Drawing.h"
#include "Project.h"
#include "RenderingSystem.h"
#include "CubicBezier.h"

#include <implot.h>

Application::Application()
{
	// Window has opengl context. First init;
	Window = std::make_unique<class Window>();
	RenderingSystem::Init();
}

void Application::Run()
{
	GenDefaultProject();
	while (!Window->ShouldClose())
	{
		Window->BeginFrame();
		ImPlot::ShowDemoWindow();
		ImGui::ShowDemoWindow();
		ActiveProject->CanvasPanel->DrawAndRunTool();
		ActiveProject->CanvasPanel->ActiveDrawing->ArrangementSystem.Run();
		ActiveProject->CanvasPanel->ActiveDrawing->Draw();

		
		Window->EndFrame();
	}
}

void Application::GenDefaultProject() 
{
	ActiveProject = std::make_unique<Project>();

	auto drawing = std::make_unique<Drawing>();
	drawing->UpperLeft = {0.0f, 0.0f};
	drawing->LowerRight = {0.297f, 0.21f};
	drawing->Dpi = 144.0f;
	drawing->GenRenderTarget();
	ActiveProject->MainDrawing = std::move(drawing);

	ActiveProject->CanvasPanel = std::make_unique<CanvasPanel>();
	ActiveProject->CanvasPanel->ActiveDrawing = ActiveProject->MainDrawing.get();
	ActiveProject->CanvasPanel->GenOverlayBuffers();
}
