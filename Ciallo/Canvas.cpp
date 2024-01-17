#include "pch.hpp"
#include "Canvas.h"
#include "CanvasEvent.h"

#include "StrokeContainer.h"
#include "Stroke.h"
#include "Brush.h"
#include "TextureManager.h"
#include "TempLayers.h"
#include "Loader.h"
#include "TimelineManager.h"

#include <glm/gtx/transform.hpp>

Canvas::Canvas()
{
	Viewport.Min = { 0.0f, 0.0f };
	Viewport.Max = { 0.297f, 0.21f };
	Dpi = 144.0f;
	GenRenderTarget();
}

Canvas::Canvas(glm::vec2 min, glm::vec2 max, float dpi)
{
	Viewport.Min = min;
	Viewport.Max = max;
	Dpi = dpi;
	GenRenderTarget();
}

void Canvas::DrawUI()
{
	const ImGuiWindowFlags flags =
		ImGuiWindowFlags_NoScrollWithMouse | ImGuiWindowFlags_HorizontalScrollbar |
		ImGuiWindowFlags_AlwaysHorizontalScrollbar | ImGuiWindowFlags_AlwaysVerticalScrollbar |
		ImGuiWindowFlags_MenuBar;

	ImGui::PushStyleVar(ImGuiStyleVar_WindowPadding, {.0f, .0f});
	ImGui::Begin("Canvas", nullptr, flags);

	ImGui::PopStyleVar();

	if (ImGui::Button("Load Model"))
	{
		Loader::LoadCsv("./models/girl.csv", 0.001f);
	}

	if (ImGui::Button("Save Project"))
	{
		Loader::SaveProject("./project/project");
	}

	if (ImGui::Button("Load Project"))
	{
		Loader::ShouldLoadProject = true;
	}

	ImGui::End();
}

void Canvas::GenRenderTarget()
{
	auto size = Viewport.GetSizePixel(Dpi);
	Image = RenderableTexture(size.x, size.y);

	int w, h;
	int miplevel = 0;
	glGetTextureLevelParameteriv(Image.ColorTexture, miplevel, GL_TEXTURE_WIDTH, &w);
	glGetTextureLevelParameteriv(Image.ColorTexture, miplevel, GL_TEXTURE_HEIGHT, &h);
}

void Canvas::RenderContentNTimes(int n)
{
	// RenderableTexture& target = Image;
	// glEnable(GL_BLEND);
	// glBlendFuncSeparate(GL_SRC_ALPHA, GL_ONE_MINUS_SRC_ALPHA, GL_ONE, GL_ONE_MINUS_SRC_ALPHA);
	// target.BindFramebuffer();
	// glClearColor(1, 1, 1, 1);
	// glClear(GL_COLOR_BUFFER_BIT);
	//
	// std::vector<Stroke*> strokes;
	// auto& strokeEs = R.ctx().get<StrokeContainer>().StrokeEs;
	// for (entt::entity strokeE : strokeEs)
	// {
	// 	strokes.push_back(&R.get<Stroke>(strokeE));
	// }
	// auto& brush = R.get<Brush>(strokes[0]->BrushE);
	//
	// std::default_random_engine rng;
	// std::uniform_real dist(-1.0f, 1.0f);
	// auto& canvas = R.ctx().get<Canvas>();
	//
	// brush.Use();
	// auto start = chrono::high_resolution_clock::now();
	// for (int i = 0; i < n; ++i)
	// {
	// 	glm::vec2 randOffset = glm::vec2(dist(rng), dist(rng)) * canvas.Viewport.GetSize() / 2.0f;
	// 	Viewport.UploadMVP(glm::translate(glm::vec3{randOffset, 0.f}));
	// 	Viewport.BindMVPBuffer();
	// 	for (auto* stroke : strokes)
	// 	{
	// 		brush.SetUniforms();
	// 		stroke->SetUniforms();
	// 		stroke->LineDrawCall();
	// 	}
	// }
	// chrono::duration<double, std::milli> duration = chrono::high_resolution_clock::now() - start;
	// spdlog::info("{}ms", duration.count());
	// glBindFramebuffer(GL_FRAMEBUFFER, 0);
	auto& strokeEs = R.ctx().get<StrokeContainer>().StrokeEs;
	auto dup = strokeEs;
	for (int i = 0; i < n; ++i)
	{
		std::copy(dup.begin(), dup.end(), std::back_inserter(strokeEs));
	}
}

glm::ivec2 Canvas::GetSizePixel() const
{
	return Viewport.GetSizePixel(Dpi);
}

void Canvas::Export() const
{
	TextureManager::SaveTexture(Image.ColorTexture, "canvas");
}

void Canvas::Run()
{
	if (ImGui::IsKeyPressed(ImGuiKey_W)) {
		if(Dpi < 1000.0f)
			Dpi *= 1.1f;
		GenRenderTarget();
		R.ctx().get<TempLayers>().GenLayers(Image.Size());
	}
	if (ImGui::IsKeyPressed(ImGuiKey_S)) {
		Dpi /= 1.1f;
		GenRenderTarget();
		R.ctx().get<TempLayers>().GenLayers(Image.Size());
	}
}
