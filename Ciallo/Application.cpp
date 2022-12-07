#include "pch.hpp"
#include "Application.h"

#include "CanvasPanel.h"
#include "Drawing.h"
#include "Project.h"
#include "RenderingSystem.h"
#include "CubicBezier.h"
#include "TextureManager.h"

#include <implot.h>

Application::Application()
{
	// Window has opengl context. First init;
	Window = std::make_unique<class Window>();
	RenderingSystem::Init();
	TextureManager::LoadTextures();
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
		ActiveProject->BrushManager->Draw();

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

	auto brushManager = std::make_unique<BrushManager>();
	auto& brushes = brushManager->Brushes;
	brushes.push_back(std::make_unique<Brush>());
	brushes[0]->Program = RenderingSystem::ArticulatedLine->Program();
	brushes[0]->Color = {0.5f, 0.5f, 0.5f, 1.0f};

	brushes.push_back(std::make_unique<Brush>());
	brushes[1]->Program = RenderingSystem::ArticulatedLine->Program(ArticulatedLineEngine::Type::Stamp);
	brushes[1]->Color = {0.5f, 0.5f, 0.5f, 1.0f};
	brushes[1]->Stamp = TextureManager::Textures[0];

	brushManager->RenderPreview();
	ActiveProject->BrushManager = std::move(brushManager);

	ActiveProject->CanvasPanel = std::make_unique<CanvasPanel>();
	ActiveProject->CanvasPanel->ActiveDrawing = ActiveProject->MainDrawing.get();
	ActiveProject->CanvasPanel->GenOverlayBuffers();
	ActiveProject->CanvasPanel->PaintTool->ActiveBrush = ActiveProject->BrushManager->Brushes[1].get();
}
