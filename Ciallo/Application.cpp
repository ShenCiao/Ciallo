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

#include <iostream>
#include <fstream>
#include <string>
#include <vector>
#include "Timer.h"

Application::Application()
{
	// window with opengl context
	Window = std::make_unique<class Window>();
	RenderingSystem::Init();
	TextureManager::LoadTextures();

	//my_version
	// collect all file name and save in one vector
	// AReadFile::ReadFile();

	// load stamp image
	// AStamp::LoadStamp(AReadFile::file_names);
	

	
	// load vector image


	// load parameter
	// std::ifstream file("C:/Users/zguan/Desktop/stamp-dataset/parameters/combination1.csv");
	// std::string line;

	// while (std::getline(file, line)) {
	// 	std::vector<std::string> row;

	// 	// Split the line into individual values
	// 	std::string value;
	// 	std::stringstream ss(line);
	// 	while (std::getline(ss, value, ',')) {
	// 		row.push_back(value);
	// 	}

	// 	// Process the row of values
	// 	for (const auto& val : row) {
	// 		std::cout << val << " ";
	// 	}
	// 	std::cout << std::endl;
	// }

	// render vector image using stamp and parameter

	// save image
}

void Application::Run()
{
	// number of sampled footprint: 1 to 100
	// radius: 0.0005, 0.00175, 0.003
	

	R.ctx().emplace<Timer>();
	GenDefaultProject();
	
	int w, h;
	glfwGetFramebufferSize(Window->GlfwWindow, &w, &h);
	glViewport(0, 0, w, h);

	
	bool shouldClose = false;
	while (!Window->ShouldClose())
	{	
		for(float ra = 0.00175f; ra < 0.004; ra += 0.001){
			for(float nof = 5.0f; nof < 51.0; nof += 5.0){
				shouldClose = true;
				Loader::LoadCsv("./models/girl.csv", ra, nof);
				auto& currTime = R.ctx().get<Timer>();
				currTime.timestamp = std::chrono::high_resolution_clock::now();
				
				while(true){
					Window->BeginFrame();
					ImGui::ShowMetricsWindow();
					R.ctx().get<Canvas>().DrawUI();

					if(currTime.run()){
						Window->EndFrame();
						break;
					}
					entt::entity currentE = R.ctx().get<TimelineManager>().GetCurrentDrawing();
					// if (currentE != entt::null)
					// {
					// 	R.get<ArrangementManager>(currentE).Run();
					// }
					auto& layers = R.ctx().get<TempLayers>();
					
					glEnable(GL_BLEND);
					glBlendFuncSeparate(GL_SRC_ALPHA, GL_ONE_MINUS_SRC_ALPHA, GL_ONE, GL_ONE_MINUS_SRC_ALPHA);
					glDisable(GL_DEPTH_TEST);
					glDisable(GL_STENCIL_TEST);
					glBindFramebuffer(GL_FRAMEBUFFER, 0);
					glClearColor(1, 1, 1, 1);
					glClear(GL_COLOR_BUFFER_BIT);

					auto& canvas = R.ctx().get<Canvas>();
					canvas.Viewport.UploadMVP();
					canvas.Viewport.BindMVPBuffer();
					glBindFramebuffer(GL_FRAMEBUFFER, 0);
					
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
		}

		if(shouldClose){
			break;
		}
	}
}

void Application::GenDefaultProject()
{

	// std::string path = "C:/Users/zguan/Desktop/stroke-universe/code/Ciallo/Ciallo/stamps";
	// std::vector<std::string> stampsFile;
    // // Iterate over all the files in the directory
    // for (const auto& entry : std::filesystem::directory_iterator(path))
    // {
    //      //Extract the file name from the path and add it to the list
    //     stampsFile.push_back(entry.path().filename().string());
    // }

	// int stampNum = stampsFile.size();
	// std::cout << "Number of stamps: " << stampNum << std::endl;

	// std::string paraPath = "C:/Users/zguan/Desktop/stroke-universe/code/Ciallo/Ciallo/parameters/combination_6Para.csv";
    // std::ifstream paraFile(paraPath);
    // std::vector<std::vector<std::string>> parameterList;
    // std::string paraLine;
    // while (std::getline(paraFile, paraLine))
    // {
    //     std::stringstream ss(paraLine);
    //     std::vector<std::string> row;
    //     std::string cell;
    //     while (std::getline(ss, cell, ','))
    //     {
    //         std::cout << cell << " ";
	// 		row.push_back(cell);
    //     }
	// 	std::cout << std::endl;
    //     parameterList.push_back(row);
    // }
    // paraFile.close();

	// user's project level "singleton" are managed by ctx()
	float ratio = 0.21 / 9.0;
	glm::vec2 min = { 0.0f, 0.0f };
	glm::vec2 max = { ratio * 16.0, ratio * 9.0 };
	float dpi = 174.17142857142858f;
	auto& canvas = R.ctx().emplace<Canvas>(min, max, dpi);
	R.ctx().emplace<TempLayers>(canvas.GetSizePixel());

	std::vector<entt::entity> brushes;
	
	int stampNum = 109;

	std::vector<int> brush_stamp {35,70,87,107};
	
	brushes.push_back(R.create());
	auto& brush0 = R.emplace<Brush>(brushes.back());
	brush0.Name = "Vanilla"; //+ std::to_string(i);
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

	// brushes.push_back(R.create());
	// auto& brush3 = R.emplace<Brush>(brushes.back());
	// brush3.Name = "Pencil";
	// brush3.Program = RenderingSystem::ArticulatedLine->Program(ArticulatedLineEngine::Type::Stamp);
	// brush3.Stamp = std::make_unique<StampBrushData>();
	// // brush2.Stamp->StampTexture = TextureManager::Textures[2];
	// brush3.Stamp->StampTexture = AStamp::Stamps[2];
	// brush3.Stamp->StampIntervalRatio = 1.0f / 5.0f;
	// brush3.Stamp->NoiseFactor = 1.7f;

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
