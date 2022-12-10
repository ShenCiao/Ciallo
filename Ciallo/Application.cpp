#include "pch.hpp"
#include "Application.h"

#include "CanvasPanel.h"
#include "Drawing.h"
#include "Project.h"
#include "RenderingSystem.h"
#include "StampBrush.h"
#include "AirBrush.h"
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

	auto brush0 = std::make_unique<Brush>();
	brush0->Name = "Vanilla";
	brush0->Program = RenderingSystem::ArticulatedLine->Program();
	brush0->Color = {0.5f, 0.5f, 0.5f, 1.0f};
	brushes.push_back(std::move(brush0));

	auto brush1 = std::make_unique<StampBrush>();
	brush1->Name = "Stamp1";
	brush1->Program = RenderingSystem::ArticulatedLine->Program(ArticulatedLineEngine::Type::Stamp);
	brush1->Color = {0.5f, 0.5f, 0.5f, 1.0f};
	brush1->Stamp = TextureManager::Textures[0];
	brush1->StampIntervalRatio = 1.0f / 5;
	brushes.push_back(std::move(brush1));

	auto brush2 = std::make_unique<StampBrush>();
	brush2->Name = "Stamp2";
	brush2->Program = RenderingSystem::ArticulatedLine->Program(ArticulatedLineEngine::Type::Stamp);
	brush2->Color = { 0.0f, 0.0f, 0.0f, 1.0f };
	brush2->Stamp = TextureManager::Textures[1];
	brush2->StampIntervalRatio = 1.0f / 5.0f;
	brushes.push_back(std::move(brush2));

	auto brush3 = std::make_unique<AirBrush>();
	brush3->Name = "Airbrush";
	brush3->Program = RenderingSystem::ArticulatedLine->Program(ArticulatedLineEngine::Type::Airbrush);
	brush3->Color = { 0.0f, 0.0f, 0.0f, 0.9f };
	brush3->Curve = glm::mat4x2{ {0.0f, 1.0f}, {0.2f, 1.0f}, {0.5f, 0.0f}, {1.0f, 0.0f} };
	brush3->UpdateGradient();
	brushes.push_back(std::move(brush3));

	brushManager->RenderPreview();
	ActiveProject->BrushManager = std::move(brushManager);

	ActiveProject->CanvasPanel = std::make_unique<CanvasPanel>();
	ActiveProject->CanvasPanel->ActiveDrawing = ActiveProject->MainDrawing.get();
	ActiveProject->CanvasPanel->GenOverlayBuffers();
	ActiveProject->CanvasPanel->PaintTool->ActiveBrush = ActiveProject->BrushManager->Brushes[3].get();
}
