#include "pch.hpp"
#include "Application.h"

#include "CanvasPanel.h"
#include "Drawing.h"
#include "Project.h"
#include "RenderingSystem.h"
#include "Brush.h"
#include "TextureManager.h"
#include "LayerManager.h"


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
		R.ctx().get<BrushManager>().Draw();
		// -----------------------------------------------------------
		auto& lm = R.ctx().get<LayerManager>();
		lm.DrawUI();
		// -----------------------------------------------------------
		Window->EndFrame();
	}
}

void Application::GenDefaultProject()
{
	ActiveProject = std::make_unique<Project>();
	ActiveProject->MainRegistry = &R;

	auto drawing = std::make_unique<Drawing>();
	drawing->UpperLeft = {0.0f, 0.0f};
	drawing->LowerRight = {0.297f, 0.21f};
	drawing->Dpi = 144.0f;
	drawing->GenRenderTarget();
	ActiveProject->MainDrawing = std::move(drawing);

	std::vector<entt::entity> brushes;
	brushes.push_back(R.create());
	auto& brush0 = R.emplace<Brush>(brushes.back());
	brush0.Name = "vanilla";
	brush0.Program = RenderingSystem::ArticulatedLine->Program();

	brushes.push_back(R.create());
	auto& brush1 = R.emplace<Brush>(brushes.back());
	brush1.Name = "splatter";
	brush1.Program = RenderingSystem::ArticulatedLine->Program(ArticulatedLineEngine::Type::Stamp);
	brush1.Stamp = std::make_unique<StampBrushData>();
	brush1.Stamp->StampTexture = TextureManager::Textures[0];
	brush1.Stamp->StampIntervalRatio = 1.0f / 5.0f;

	brushes.push_back(R.create());
	auto& brush2 = R.emplace<Brush>(brushes.back());
	brush2.Name = "pencil";
	brush2.Program = RenderingSystem::ArticulatedLine->Program(ArticulatedLineEngine::Type::Stamp);
	brush2.Stamp = std::make_unique<StampBrushData>();
	brush2.Stamp->StampTexture = TextureManager::Textures[1];
	brush2.Stamp->StampIntervalRatio = 1.0f / 5.0f;

	brushes.push_back(R.create());
	auto& brush3 = R.emplace<Brush>(brushes.back());
	brush3.Name = "airbrush";
	brush3.Program = RenderingSystem::ArticulatedLine->Program(ArticulatedLineEngine::Type::Airbrush);
	brush3.AirBrush = std::make_unique<AirBrushData>();
	brush3.AirBrush->Curve = glm::mat4x2{ {0.0f, 1.0f}, {0.2f, 1.0f}, {0.5f, 0.0f}, {1.0f, 0.0f} };
	brush3.AirBrush->UpdateGradient();

	auto& manager = R.ctx().emplace<BrushManager>();
	manager.Brushes = std::move(brushes);
	manager.RenderAllPreview();

	ActiveProject->CanvasPanel = std::make_unique<CanvasPanel>();
	ActiveProject->CanvasPanel->ActiveDrawing = ActiveProject->MainDrawing.get();
	ActiveProject->CanvasPanel->GenOverlayBuffers();
	// ActiveProject->CanvasPanel->PaintTool->ActiveBrush = ActiveProject->BrushManager->Brushes[3].get();

	auto& lm = R.ctx().emplace<LayerManager>();
}
