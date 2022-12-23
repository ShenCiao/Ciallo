#include "pch.hpp"
#include "Application.h"

#include "Project.h"
#include "RenderingSystem.h"
#include "Brush.h"
#include "TextureManager.h"
#include "LayerManager.h"
#include "Canvas.h"

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
		R.ctx().get<Canvas>().DrawUI();
		R.ctx().get<BrushManager>().DrawUI();
		R.ctx().get<LayerManager>().DrawUI();
		Window->EndFrame();
	}
}

void Application::GenDefaultProject()
{
	ActiveProject = std::make_unique<Project>();
	ActiveProject->MainRegistry = &R;

	auto& canvas = R.ctx().emplace<Canvas>();
	canvas.Viewport.Min = {0.0f, 0.0f};
	canvas.Viewport.Max = {0.297f, 0.21f};
	canvas.Dpi = 144.0f;

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

	auto& brushManager = R.ctx().emplace<BrushManager>();
	brushManager.Brushes = std::move(brushes);
	brushManager.RenderAllPreview();


	auto& lm = R.ctx().emplace<LayerManager>();
}
