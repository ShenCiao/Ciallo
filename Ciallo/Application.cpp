#include "pch.hpp"
#include "Application.h"

#include <filesystem>

#include "BrushManager.h"
#include "RenderingSystem.h"
#include "Brush.h"
#include "TextureManager.h"
#include "Canvas.h"
#include "TempLayers.h"
#include "StrokeContainer.h"
#include "Toolbox.h"
#include "InnerBrush.h"
#include "ArrangementManager.h"
#include "TimelineManager.h"
#include "SelectionManager.h"
#include "Loader.h"

Application::Application()
{
	// window with opengl context
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
		ImGui::ShowMetricsWindow();
		R.ctx().get<Canvas>().DrawUI();
		R.ctx().get<BrushManager>().DrawUI();
		R.ctx().get<Toolbox>().DrawUI();
		R.ctx().get<TimelineManager>().DrawUI();
		entt::entity currentE = R.ctx().get<TimelineManager>().GetCurrentDrawing();
		if (currentE != entt::null)
		{
			R.get<ArrangementManager>(currentE).Run();
		}
		auto& layers = R.ctx().get<TempLayers>();
		layers.RenderOverlay();
		layers.RenderDrawing();
		layers.RenderFill();
		layers.BlendAll();
		layers.ClearOverlay();
		R.ctx().get<SelectionManager>().RenderSelectionTexture();
		Window->EndFrame();

		if (Loader::ShouldLoadProject)
		{
			glfwSwapBuffers(Window->GlfwWindow);
			Loader::LoadProject("./project/project");
			Loader::ShouldLoadProject = false;
		}

		if (auto& tm = R.ctx().get<TimelineManager>(); tm.ExportingIndex >= 0) {
			
			auto& canvas = R.ctx().get<Canvas>();
			TextureManager::SaveTexture(canvas.Image.ColorTexture, std::to_string(R.ctx().get<TimelineManager>().CurrentFrame));
			if (tm.ExportingIndex >= tm.KeyFrames.size()) {
				tm.ExportingIndex = -1;
			}
		}
	}
}

void Application::GenDefaultProject()
{
	// user's project level "singleton" are managed by ctx()
	auto& canvas = R.ctx().emplace<Canvas>();
	R.ctx().emplace<TempLayers>(canvas.GetSizePixel());

	std::vector<entt::entity> brushes;
	brushes.push_back(R.create());
	auto& brush0 = R.emplace<Brush>(brushes.back());
	brush0.Name = "Vanilla";
	brush0.Program = RenderingSystem::ArticulatedLine->Program();

	brushes.push_back(R.create());
	auto& brush1 = R.emplace<Brush>(brushes.back());
	brush1.Name = "Splatter";
	brush1.Program = RenderingSystem::ArticulatedLine->Program(ArticulatedLineEngine::Type::Stamp);
	brush1.Stamp = std::make_unique<StampBrushData>();
	brush1.Stamp->StampTexture = TextureManager::Textures[1];
	brush1.Stamp->StampIntervalRatio = 1.0f / 5.0f;

	brushes.push_back(R.create());
	auto& brush2 = R.emplace<Brush>(brushes.back());
	brush2.Name = "Pencil";
	brush2.Program = RenderingSystem::ArticulatedLine->Program(ArticulatedLineEngine::Type::Stamp);
	brush2.Stamp = std::make_unique<StampBrushData>();
	brush2.Stamp->StampTexture = TextureManager::Textures[2];
	brush2.Stamp->StampIntervalRatio = 1.0f / 10.0f;
	brush2.Stamp->NoiseFactor = 1.7f;

	brushes.push_back(R.create());
	auto& brush3 = R.emplace<Brush>(brushes.back());
	brush3.Name = "Airbrush";
	brush3.Program = RenderingSystem::ArticulatedLine->Program(ArticulatedLineEngine::Type::Airbrush);
	brush3.AirBrush = std::make_unique<AirBrushData>();
	brush3.AirBrush->Curve = glm::mat4x2{ {0.0f, 1.0f}, {0.2f, 1.0f}, {0.5f, 0.0f}, {1.0f, 0.0f} };
	brush3.AirBrush->UpdateGradient();

	brushes.push_back(R.create());
	auto& brush4 = R.emplace<Brush>(brushes.back());
	brush4.Name = "Dot";
	brush4.Program = RenderingSystem::ArticulatedLine->Program(ArticulatedLineEngine::Type::Stamp);
	brush4.Stamp = std::make_unique<StampBrushData>();
	brush4.Stamp->StampTexture = TextureManager::Textures[3];
	brush4.Stamp->StampIntervalRatio = 1.0f / 5.0f;
	brush4.Stamp->RotationRand = 0.0f;

	auto& brushManager = R.ctx().emplace<BrushManager>();
	brushManager.Brushes = std::move(brushes);
	brushManager.RenderAllPreview();

	R.ctx().emplace<OverlayContainer>();
	R.ctx().emplace<InnerBrush>();
	R.ctx().emplace<Toolbox>(); 
	auto& tm = R.ctx().emplace<TimelineManager>();
	tm.GenKeyFrame(1);
	R.ctx().emplace<SelectionManager>(); // Depend on Canvas
}
