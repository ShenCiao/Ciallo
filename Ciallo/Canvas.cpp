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
#include<windows.h>

#include <random>
#include <fstream>
#include <sstream>
#include <filesystem>
#include <glm/gtx/transform.hpp>
#include <chrono>

// #include <filesystem>
// namespace fs = std::filesystem;

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
	ImGui::BeginMenuBar();

	if (ImGui::BeginMenu("Load Model"))
	{
		for(auto& model : csv_paths){
			std::string menuName = model;
			const char * c = menuName.c_str();
			if (ImGui::MenuItem(c)){
				Loader::LoadCsv(model);
				std::cout << menuName << std::endl;
			}
		}

		// if (ImGui::MenuItem("horizontal-straight")){
		// 	Loader::LoadCsv("./patch-paths/horizontal-straight.csv");
		// }
		ImGui::EndMenu();
	}
		// if (ImGui::MenuItem("Monkey"))
		// 	Loader::LoadCsv("./csv/DIY_Gantry_bust_010_RGB_p90.csv");
		// if (ImGui::MenuItem("Bear"))
		// 	Loader::LoadCsv("./noPressure_csv/DIY_Gantry_bust_010_RGB_p100.csv");
		// if (ImGui::MenuItem("Dango"))
		// 	// Loader::LoadCsv("./csv/DIY_Gantry_bust_010_RGB_p104.csv", 0.003f);
		// 	Loader::LoadCsv("./noPressure_csv/DIY_Gantry_bust_010_RGB_p104.csv");
		// if (ImGui::MenuItem("Totoro"))
		// 	// Loader::LoadCsv("./csv/DIY_Gantry_bust_010_RGB_p105.csv", 0.001f);
		// 	Loader::LoadCsv("./noPressure_csv/DIY_Gantry_bust_010_RGB_p105.csv");
		// if (ImGui::MenuItem("Grid"))
		// 	Loader::LoadCsv("./noPressure_csv/DIY_Gantry_bust_010_RGB_p110.csv");

		// if (ImGui::MenuItem("Monkey"))
		// 	Loader::LoadCsv("./models/monkey.csv");
		// if (ImGui::MenuItem("Bear"))
		// 	Loader::LoadCsv("./models/bear.csv");
		// if (ImGui::MenuItem("Dango"))
		// 	Loader::LoadCsv("./models/dango.csv", 0.003f);
		// if (ImGui::MenuItem("Totoro"))
		// 	Loader::LoadCsv("./models/totoro.csv", 0.001f);
		// if (ImGui::MenuItem("Grid"))
		// 	Loader::LoadCsv("./models/grid.csv");

	if (ImGui::BeginMenu("Layers"))
	{
		auto& layers = R.ctx().get<TempLayers>();
		ImGui::Checkbox("Final only", &layers.FinalOnly);
		ImGui::Checkbox("Hide Fill", &layers.HideFill);
		ImGui::EndMenu();
	}

	// // Set the image size to the new canvas size
	// Image.Width = 1684/2;
	// Image.Height = 1191/2;

	if (ImGui::Button("Export")) Export();
	static int n = 1;
	ImGui::PushItemWidth(200);
	ImGui::DragInt("n", &n, 10, 1, 10000, "%d");
	ImGui::PopItemWidth();
	if (ImGui::Button("TestSpeed")) RenderContentNTimes(n);
	if (ImGui::Button("Clear")) ClearCanvas();
	ImGui::EndMenuBar();

	ImGui::PushStyleVar(ImGuiStyleVar_WindowPadding, {.0f, .0f});
	auto panel = ImGui::GetCurrentWindow();
	glm::vec2 drawingScreenOrigin = ImGui::GetCursorScreenPos();

	ImGui::Image(ImTextureID(Image.ColorTexture), Image.Size());
	// mouse position relative to image
	glm::vec2 mousePosPixel = ImGui::GetMousePos() - drawingScreenOrigin;
	glm::vec2 mousePos = mousePosPixel / (Viewport.GetSizePixelFloat(Dpi) * Zoom) * Viewport.GetSize();

	// std::cout << "drawingScreenOrigin " << drawingScreenOrigin[0] << " " << drawingScreenOrigin[1] << std::endl;
	// std::cout << "ImGui::GetMousePos() " << ImGui::GetMousePos()[0] << " " << ImGui::GetMousePos()[1] << std::endl;
	// std::cout << "mousePosPixel " << mousePosPixel[0] << " " << mousePosPixel[1] << std::endl;
	// std::cout << "Zoom " << Zoom << std::endl;
	// std::cout << "Viewport size " << Viewport.GetSize()[0] << " " << Viewport.GetSize()[1] << std::endl;
	// std::cout << "Viewport size " << Viewport.GetSizePixelFloat(Dpi)[0] << " " << Viewport.GetSizePixelFloat(Dpi)[1] << std::endl;

	// std::cout << "mousePos " << mousePos[0] << " " << mousePos[1] << std::endl;

	// Invisible button for interaction
	ImGui::SetCursorScreenPos(panel->InnerRect.Min);
	ImGui::InvisibleButton("CanvasInteraction", panel->InnerRect.GetSize(),
	                       ImGuiButtonFlags_MouseButtonLeft | ImGuiButtonFlags_MouseButtonRight |
	                       ImGuiButtonFlags_MouseButtonMiddle);
	EventDispatcher.trigger(BeforeInteraction{});

	auto interact = [&]()
	{
		if (ImGui::IsMouseClicked(0) && ImGui::IsItemActivated())
		{
			if (IsDragging)
			{
				IsDragging = false;
				auto duration = chrono::high_resolution_clock::now() - StartDraggingTimePoint;
				EventDispatcher.trigger(DragEnd{
					mousePos, mousePosPixel, PrevMousePos - mousePos, ImGui::GetMouseDragDelta(), duration
				});
			}
			StartDraggingTimePoint = chrono::high_resolution_clock::now();
			EventDispatcher.trigger(ClickOrDragStart{mousePos, mousePosPixel});
			PrevMousePos = mousePos;
			return;
		}

		if (ImGui::IsMouseDragging(0) && !ImGui::IsItemActivated() && ImGui::IsItemActive())
		{
			IsDragging = true;
			auto duration = chrono::high_resolution_clock::now() - StartDraggingTimePoint;
			EventDispatcher.trigger(Dragging{
				mousePos, mousePosPixel, mousePos - PrevMousePos, ImGui::GetMouseDragDelta(), duration
			});
			PrevMousePos = mousePos;
			return;
		}

		if (IsDragging && !ImGui::IsMouseDragging(0))
		{
			IsDragging = false;
			auto duration = chrono::high_resolution_clock::now() - StartDraggingTimePoint;
			EventDispatcher.trigger(DragEnd{
				mousePos, mousePosPixel, mousePos - PrevMousePos, ImGui::GetMouseDragDelta(), duration
			});
			PrevMousePos = mousePos;
			return;
		}

		if (ImGui::IsItemHovered() && !IsDragging && !ImGui::IsMouseClicked(0))
		{
			EventDispatcher.trigger(Hovering{mousePos, mousePosPixel});
		}
	};

	interact();
	EventDispatcher.trigger(AfterInteraction{});
	ImGui::End();
	ImGui::PopStyleVar();
}

void Canvas::GenRenderTarget()
{
	auto size = Viewport.GetSizePixel(Dpi);
	Image = RenderableTexture(size.x, size.y);
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
	for(int i = 0; i <n; ++i)
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

void Canvas::ExportNew(std::string exportPath) const
{
	TextureManager::SaveTexture(Image.ColorTexture, exportPath);
}

void Canvas::ClearCanvas() const
{

	R.ctx().get<StrokeContainer>().StrokeEs.clear();
	auto& arm = R.ctx().get<ArrangementManager>();
	arm.Arrangement.clear();
}