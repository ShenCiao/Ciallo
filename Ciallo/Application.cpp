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
#include "EyedropperInfo.h"
#include "Painter.h"

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
		entt::entity currentE = R.ctx().get<TimelineManager>().GetCurrentDrawing();
		if (currentE != entt::null)
		{
			R.get<ArrangementManager>(currentE).Run();
		}
		auto& layers = R.ctx().get<TempLayers>();
		
		glEnable(GL_BLEND);
		glBlendFuncSeparate(GL_SRC_ALPHA, GL_ONE_MINUS_SRC_ALPHA, GL_ONE, GL_ONE_MINUS_SRC_ALPHA);
		glDisable(GL_DEPTH_TEST);
		glDisable(GL_STENCIL_TEST);
		glBindFramebuffer(GL_FRAMEBUFFER, 0);
		glClearColor(1, 1, 1, 1);
		glClear(GL_COLOR_BUFFER_BIT);

		Viewport viewport{ { 0.0f, 0.0f }, { 0.297f, 0.21f } };

		auto& canvas = R.ctx().get<Canvas>();
		canvas.Viewport.UploadMVP();
		canvas.Viewport.BindMVPBuffer();
		glBindFramebuffer(GL_FRAMEBUFFER, 0);
		int w, h;
		glfwGetFramebufferSize(Window->GlfwWindow, &w, &h);
		glViewport(0, 0, w, h);
		entt::entity drawingE = R.ctx().get<TimelineManager>().GetRenderDrawing();
		if (drawingE == entt::null) return;
		auto& strokeEs = R.get<StrokeContainer>(drawingE).StrokeEs;

		for (entt::entity e : strokeEs)
		{
			auto& stroke = R.get<Stroke>(e);
			auto strokeUsage = R.get<StrokeUsageFlags>(e);
			bool skip = false;
			//bool skip = !(strokeUsage & StrokeUsageFlags::Zone);// marker only
			if (!skip)
			{
				Brush* brush;
				if (!!(strokeUsage & StrokeUsageFlags::Zone)) {
					if (stroke.Position.size() <= 1) {
						brush = &R.ctx().get<InnerBrush>().Get("fill_marker");
					}
					else {
						brush = &R.ctx().get<InnerBrush>().Get("vanilla");
					}
				}
				else
					brush = &R.get<Brush>(stroke.BrushE);
				brush->Use();
				brush->SetUniforms();
				stroke.SetUniforms();
				stroke.LineDrawCall();
			}
		}


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
	float ratio = 0.21 / 9.0;
	glm::vec2 min = { 0.0f, 0.0f };
	glm::vec2 max = { ratio * 16.0, ratio * 9.0 };
	float dpi = 100.0f;
	auto& canvas = R.ctx().emplace<Canvas>(min, max, dpi);
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
	brush2.Stamp->StampIntervalRatio = 0.25f;
	brush2.Stamp->NoiseFactor = 1.9f;

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
	brush4.Stamp->StampTexture = TextureManager::Textures[6];
	brush4.Stamp->StampIntervalRatio = 1.0f / 5.0f;
	brush4.Stamp->RotationRand = 0.0f;

	R.ctx().emplace<InnerBrush>();
	auto& brushManager = R.ctx().emplace<BrushManager>();
	brushManager.Brushes = std::move(brushes);
	brushManager.RenderAllPreview();

	R.ctx().emplace<OverlayContainer>();
	R.ctx().emplace<Toolbox>(); 
	auto& tm = R.ctx().emplace<TimelineManager>();
	tm.GenKeyFrame(1);
	R.ctx().emplace<SelectionManager>(); // Depend on Canvas
	R.ctx().emplace<EyedropperInfo>();
}
