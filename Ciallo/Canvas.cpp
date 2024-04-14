#include "pch.hpp"
#include "Canvas.h"
#include "CanvasEvent.h"

#include "StrokeContainer.h"
#include "ArrangementManager.h"
#include "Stroke.h"
#include "Brush.h"
#include "TextureManager.h"
#include "TempLayers.h"
#include "Loader.h"
#include "TimelineManager.h"

#include <glm/gtx/transform.hpp>
#include <chrono>
#include <imgui.h>  
#include <dos.h> //for delay
#include <thread>
#include "Timer.h"

// #include <filesystem>
// namespace fs = std::filesystem;

Canvas::Canvas()
{
	Viewport.Min = { 0.0f, 0.0f };
	Viewport.Max = { 0.297f, 0.21f };	// resolution
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

	std::vector<std::string> csv_paths;
	std::string csv_path;
	std::string folder_path = "C:/Users/zguan/Desktop/stroke-universe/code/Ciallo/Ciallo/figure_csv";
	// std::string folder_path = "C:/Users/zguan/Desktop/stroke-universe/code/Ciallo/Ciallo/patch_csv";
	for (const auto& entry : std::filesystem::directory_iterator(folder_path))
	{
		csv_path = "./figure_csv/" + entry.path().filename().string();
		// csv_path = "./patch_csv/" + entry.path().filename().string();
		csv_paths.push_back(csv_path);
		
	}

	
	// std::string one_csv;
	// for (auto& csv : csv_paths){
	// 	one_csv = "./csv/" + csv;
	// 	Loader::LoadCsv(one_csv); //pass the brush number

	// 	Export();
	// }

	// std::cout << csv_paths.size() << std::endl;
	// system("pause");


	ImGui::PushStyleVar(ImGuiStyleVar_WindowPadding, {.0f, .0f});
	ImGui::Begin("Canvas", nullptr, flags);

	ImGui::PopStyleVar();

	if (ImGui::Button("Load Model"))
	{
		// for(float i = 0.0001f; i < 0.001; i+=0.0001){
		// 	for(float r = 0.002; r < 0.007; r += 0.002){
				float i = 0.01f;
				float r = 0.001f;
				spdlog::info("Stamp interval: {}", i);
				spdlog::info("Target Radius: {}", r);
				Loader::LoadCsv("./models/girl.csv", r, i);
				auto& currTime = R.ctx().get<Timer>();
				currTime.timestamp = std::chrono::high_resolution_clock::now();

				// std::this_thread::sleep_for(std::chrono::milliseconds(5000));

				// ImGuiIO io = ImGui::GetIO();
				// spdlog::info("ImGui FPS: {}", io.Framerate);
				
				

		// 	}
		// }
	}

	if (ImGui::Button("Save Project"))
	{
		Loader::SaveProject("./project/project");
	}

	if (ImGui::Button("Load Project"))
	{
		Loader::ShouldLoadProject = true;
	}
	if (ImGui::Button("Print FPS")){

		

		auto start = std::chrono::steady_clock::now(); // Start the timer

		for (int count = 0; count < 5; count++) {
			std::this_thread::sleep_for(std::chrono::seconds(1)); // Wait for 1 second

			auto end = std::chrono::steady_clock::now(); // Stop the timer
			auto duration = std::chrono::duration_cast<std::chrono::milliseconds>(end - start).count(); // Calculate the duration in milliseconds
			float fps = 1000.0f / duration; // Calculate the FPS

			spdlog::info("Counted FPS: {}", fps);

			start = std::chrono::steady_clock::now(); // Restart the timer
		}
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

	const auto p1 = std::chrono::system_clock::now();
	std::string saveName = "canvas" + std::to_string(std::chrono::duration_cast<std::chrono::seconds>(p1.time_since_epoch()).count());
	TextureManager::SaveTexture(Image.ColorTexture, saveName);
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
