#include "pch.hpp"
#include "Application.h"

#include <filesystem>

#include "Project.h"
#include "RenderingSystem.h"
#include "Brush.h"
#include "TextureManager.h"
#include "Canvas.h"
#include "TempLayers.h"
#include "StrokeContainer.h"
#include "Toolbox.h"
#include "InnerBrush.h"
#include "ArrangementManager.h"

#include "AReadFile.h"
#include "AStamp.h"
#include "Loader.h"

#include <implot.h>

#include <iostream>
#include <fstream>
#include <string>
#include <vector>

Application::Application()
{
	// window with opengl context
	Window = std::make_unique<class Window>();
	RenderingSystem::Init();
	TextureManager::LoadTextures();

	// collect all file name and save in one vector
	AReadFile::ReadFile();

	// load stamp image
	AStamp::LoadStamp(AReadFile::file_names);
	
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
	GenDefaultProject();
	
	int count = 0;
	std::vector<std::string> oneData;
	std::vector<std::vector<std::string>> allData;

	while (!Window->ShouldClose())
	{
		
		std::vector<std::string> csv_paths;
		std::string csv_path;
		std::string folder_path = "C:/Users/zguan/Desktop/stroke-universe/code/Ciallo/Ciallo/figure_csv";
		for (const auto& entry : std::filesystem::directory_iterator(folder_path))
		{
			csv_path = entry.path().filename().string();
			csv_paths.push_back(csv_path);
			
		}

		// std::vector<int> brush_stamp {14,63,87,107,108}; //14,63,87,107,

		// if(count == (csv_paths.size() * brush_stamp.size())){ //107
		// 	break;
		// }

		// for(int i = 0; i < brush_stamp.size(); i++){
		// 	for (auto& each_path : csv_paths){
		// 		Window->BeginFrame();
		// 		ImGui::ShowMetricsWindow();
		// 		R.ctx().get<Canvas>().DrawUI();
		// 		int bn = brush_stamp[i];

		// 		float tt;
		// 		if(bn == 107){
		// 			tt = 0.0008;
		// 		}else{
		// 			tt = 0.0013;
		// 		}
				
		// 		// Loader::LoadCsv(each_path,bn,rr,ir,nf,tt);
		// 		Loader::LoadCsv("./figure_csv/" + each_path,bn,0.1,0.1,0.02,tt);
		// 		R.ctx().get<BrushManager>().DrawUI();
		// 		R.ctx().get<Toolbox>().DrawUI();
		// 		// R.ctx().get<ArrangementManager>().Run();
		// 		auto& layers = R.ctx().get<TempLayers>();
		// 		layers.RenderOverlay();
		// 		layers.RenderDrawing();
		// 		layers.RenderFill();
		// 		layers.BlendAll();
		// 		layers.ClearOverlay();

		// 		// std::string prefix = "./figure_csv/";
		// 		// int prefixLen = prefix.size();
		// 		// std::string saveName = each_path.substr(prefixLen);
		// 		std::string nameTemp = each_path.substr(0,each_path.size() - 4);
		// 		std::string exportPath = nameTemp + "_stamp_" + std::to_string(bn);
		// 		// std::string exportPath = nameTemp + "_bn_" + std::to_string(9) + "_sm_" + std::to_string(0) + "_rr_" + std::to_string(2.0) + "_ir_" + std::to_string(0.06) + "_nf_" + std::to_string(0.0) + "_tt_" + std::to_string(0.002);
		// 		std::cout << exportPath << std::endl;
		// 		R.ctx().get<Canvas>().ExportNew(exportPath);
		// 		Window->EndFrame();

		// 		count++;
		// 	}
		// }

		// for(int bn = 0; bn < 107; bn++){											// brush number
		// 	float rr = 0.0;
		// 	float ir = 0.18;
		// 	float nf = 0.01;
		// 	float tt = 0.002;
		// 	// for(int sm = 0; sm < 2; sm++){ 										// stamp mode
		// 	// 	for(float rr = 0; rr <= 2; rr += 0.5){ 							// rotation randomness
		// 	// 		for(float ir = 0; ir <= 2; ir += 0.5){ 						// interval ratio
		// 	// 			for(float nf = 0; nf <= 2; nf += 0.5){ 					// noise factor
		// 	// 				for(float tt = 0.001; tt <= 0.003; tt += 0.0005){	// target thickness
		// 						Window->BeginFrame();
		// 						ImGui::ShowMetricsWindow();
		// 						R.ctx().get<Canvas>().DrawUI();
								
		// 						std::string each_path = "./figure_csv/WEB_CUHK_lake_000_RGB_p28.csv";
		// 						Loader::LoadCsv(each_path,bn,rr,ir,nf,tt);
		// 						// Loader::LoadCsv(each_path,9,0,2.0,0.06,0.0,0.002);
		// 						R.ctx().get<BrushManager>().DrawUI();
		// 						R.ctx().get<Toolbox>().DrawUI();
		// 						// R.ctx().get<ArrangementManager>().Run();
		// 						auto& layers = R.ctx().get<TempLayers>();
		// 						layers.RenderOverlay();
		// 						layers.RenderDrawing();
		// 						layers.RenderFill();
		// 						layers.BlendAll();
		// 						layers.ClearOverlay();

		// 						std::string prefix = "./figure_csv/";
		// 						int prefixLen = prefix.size();
		// 						std::string saveName = each_path.substr(prefixLen);
		// 						std::string nameTemp = saveName.substr(0,saveName.size() - 4);
		// 						std::string exportPath = nameTemp + "_bn_" + std::to_string(bn) + "_rr_" + std::to_string(rr) + "_ir_" + std::to_string(ir) + "_nf_" + std::to_string(nf) + "_tt_" + std::to_string(tt);
		// 						// std::string exportPath = nameTemp + "_bn_" + std::to_string(9) + "_sm_" + std::to_string(0) + "_rr_" + std::to_string(2.0) + "_ir_" + std::to_string(0.06) + "_nf_" + std::to_string(0.0) + "_tt_" + std::to_string(0.002);
		// 						std::cout << exportPath << std::endl;
		// 						R.ctx().get<Canvas>().ExportNew(exportPath);
		// 						Window->EndFrame();

		// 						count++;
		// 	// 				}
		// 	// 			}
		// 	// 		}
		// 	// 	}
		// 	// }
		// }
		
		// for(int i = 0.0005; i < 0.003; i+=0.0005){
			// for (auto& each_path : csv_paths){
			// 	count++;
			// 	Window->BeginFrame();
			// 	ImGui::ShowMetricsWindow();
			// 	R.ctx().get<Canvas>().DrawUI();
			// 	Loader::LoadCsv("./figure_csv/" + each_path);
			// 	R.ctx().get<BrushManager>().DrawUI();
			// 	R.ctx().get<Toolbox>().DrawUI();
			// 	R.ctx().get<ArrangementManager>().Run();
			// 	auto& layers = R.ctx().get<TempLayers>();
			// 	layers.RenderOverlay();
			// 	layers.RenderDrawing();
			// 	layers.RenderFill();
			// 	layers.BlendAll();
			// 	layers.ClearOverlay();

			// 	std::string nameTemp = each_path.substr(0,each_path.size() - 4);
			// 	std::string exportPath = nameTemp + "_tarThick";// + std::to_string(i);
			// 	std::cout << exportPath << std::endl;
			// 	R.ctx().get<Canvas>().ExportNew(exportPath);

			// 	oneData = {};
			// 	oneData.push_back(nameTemp); //PNG name
			// 	oneData.push_back(nameTemp);	
			// 	oneData.push_back(std::to_string(brushT.Stamp->NoiseFactor));
			// 	oneData.push_back(std::to_string(brushT.Stamp->RotationRand));
			// 	oneData.push_back(std::to_string(brushT.Stamp->StampIntervalRatio));
			// 	oneData.push_back(std::to_string(brushT.Stamp->StampMode));

			// 	Window->EndFrame();


			// }
		// }

		// std::ofstream saveFile("figure_csv/par.csv");

		// std::vector<std::string> headline{"PNG name", "Stamp image", "Thickness", "Noise factor", "Rotation randomness", "Interval", "Stamp mode"};
		// for (const auto& line : headline){
		// 	saveFile << line << ",";
		// }
		// saveFile << std::endl;

		// for (const auto& row : pngData)
		// {
		// 	for (const auto& cell : row)
		// 	{
		// 		saveFile << cell << ",";
		// 	}
		// 	saveFile << std::endl;
		// }
		// saveFile.close();

		Window->BeginFrame();
		ImGui::ShowMetricsWindow();
		R.ctx().get<Canvas>().DrawUI();
		R.ctx().get<BrushManager>().DrawUI();
		R.ctx().get<Toolbox>().DrawUI();
		// R.ctx().get<ArrangementManager>().Run();
		auto& layers = R.ctx().get<TempLayers>();
		layers.RenderOverlay();
		layers.RenderDrawing();
		layers.RenderFill();
		layers.BlendAll();
		layers.ClearOverlay();
		Loader::DrawUI();
		Window->EndFrame();
		
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
	auto& canvas = R.ctx().emplace<Canvas>();
	canvas.Viewport.Min = {0.0f, 0.0f};
	// canvas.Viewport.Max = {0.053f, 0.053f};
	canvas.Viewport.Max = {0.297f, 0.21f}; //orginal
	// canvas.Viewport.Max = {0.297f * 0.4, 0.21f * 0.4};
	canvas.Dpi = 288.0f;
	canvas.GenRenderTarget();
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
	brush1.Name = "Airbrush"; //+ std::to_string(i);;
	brush1.Program = RenderingSystem::ArticulatedLine->Program(ArticulatedLineEngine::Type::Airbrush);
	brush1.AirBrush = std::make_unique<AirBrushData>();
	brush1.AirBrush->Curve = glm::mat4x2{ {0.0f, 1.0f}, {0.30f, 0.7f}, {0.5f, 0.0f}, {1.0f, 0.0f} };
	brush1.AirBrush->UpdateGradient();

	// all brushes should be built here. airbrushes with 11 thicknesses, 410 for each, vanilla with 11 thicknesses, 410 for each
	// int stampCount = 1;
	for (int i = 1; i <= stampNum; i++){
	// for (int i = 0; i < brush_stamp.size(); i++){
		// for (int j = 0; j < stampNum; j++){
			// if(brush_stamp[j] == (i-1)){
				brushes.push_back(R.create());
				auto& eachBrush = R.emplace<Brush>(brushes.back());
				eachBrush.Name = "Stamp" + std::to_string(i);
				// eachBrush.Name = "Stamp" + std::to_string(brush_stamp[i]);
	
				eachBrush.Program = RenderingSystem::ArticulatedLine->Program(ArticulatedLineEngine::Type::Stamp);
				eachBrush.Stamp = std::make_unique<StampBrushData>();
				// brush1.Stamp->StampTexture = TextureManager::Textures[1];
				eachBrush.Stamp->StampTexture = AStamp::Stamps[i];
				eachBrush.Stamp->StampIntervalRatio = 0.5f / 5.0f;
				eachBrush.Stamp->NoiseFactor = 0.2;
				eachBrush.Stamp->RotationRand = 1.5;
				eachBrush.Stamp->StampMode = StampBrushData::EquiDistant;
				// eachBrush.Stamp->NoiseFactor = noise;
				// eachBrush.Stamp->RotationRand = rotation;
				// if(distant){
				// 	eachBrush.Stamp->StampMode = StampBrushData::EquiDistant;
				// }else{
				// 	eachBrush.Stamp->StampMode = StampBrushData::RatioDistant;
				// }
				// stampCount++;
			// }
		// }
		
	}

	// brushes.push_back(R.create());
	// auto& brush2 = R.emplace<Brush>(brushes.back());
	// brush2.Name = "Splatter";
	// brush2.Program = RenderingSystem::ArticulatedLine->Program(ArticulatedLineEngine::Type::Stamp);
	// brush2.Stamp = std::make_unique<StampBrushData>();
	// // brush1.Stamp->StampTexture = TextureManager::Textures[1];
	// brush2.Stamp->StampTexture = AStamp::Stamps[1];
	// brush2.Stamp->StampIntervalRatio = 1.0f / 5.0f;

	// brushes.push_back(R.create());
	// auto& brush3 = R.emplace<Brush>(brushes.back());
	// brush3.Name = "Pencil";
	// brush3.Program = RenderingSystem::ArticulatedLine->Program(ArticulatedLineEngine::Type::Stamp);
	// brush3.Stamp = std::make_unique<StampBrushData>();
	// // brush2.Stamp->StampTexture = TextureManager::Textures[2];
	// brush3.Stamp->StampTexture = AStamp::Stamps[2];
	// brush3.Stamp->StampIntervalRatio = 1.0f / 5.0f;
	// brush3.Stamp->NoiseFactor = 1.7f;

	// brushes.push_back(R.create());
	// auto& brush4 = R.emplace<Brush>(brushes.back());
	// brush4.Name = "Dot";
	// brush4.Program = RenderingSystem::ArticulatedLine->Program(ArticulatedLineEngine::Type::Stamp);
	// brush4.Stamp = std::make_unique<StampBrushData>();
	// // brush4.Stamp->StampTexture = TextureManager::Textures[3];
	// brush4.Stamp->StampTexture = AStamp::Stamps[3];
	// brush4.Stamp->StampIntervalRatio = 1.0f / 5.0f;
	// brush4.Stamp->RotationRand = 0.0f;

	auto& brushManager = R.ctx().emplace<BrushManager>();
	brushManager.Brushes = std::move(brushes);
	brushManager.RenderAllPreview();

	R.ctx().emplace<StrokeContainer>();
	R.ctx().emplace<OverlayContainer>();
	R.ctx().emplace<InnerBrush>();
	R.ctx().emplace<Toolbox>();
	R.ctx().emplace<ArrangementManager>();

	// R.ctx().get<Canvas>().Export();
}
