#include "pch.hpp"
#include "Loader.h"

#include <fstream>
#include <sstream>
#include <vector>
#include <string>
#include "Polyline.h"
#include "Canvas.h"
#include "Stroke.h"
#include "BrushManager.h"
#include "StrokeContainer.h"
#include "Painter.h"
#include "ArrangementManager.h"
#include "Brush.h"
#include <random>
#include <algorithm>


void Loader::LoadCsv(const std::filesystem::path& filePath, int brushNum, float rotateRandom, float intervalRatio, float noiseFactor, float targetThickness) //, int brushNum, int iterate, entt::entity brushE
{
	// Warning: memory leak! (not trying to remove unused stroke)
	R.ctx().get<StrokeContainer>().StrokeEs.clear();
	auto& arm = R.ctx().get<ArrangementManager>();
	arm.Arrangement.clear();
	arm.LogSpeed = true;

	std::ifstream file(filePath);
	std::string line;
	file.exceptions(std::ifstream::badbit);

	Geom::Polyline curve, allPoints;
	std::vector<Geom::Polyline> curves;
	std::vector<std::vector<float>> pressures; // pen pressures get from gpencil
	std::vector<float> pressure;
	std::vector<int> all_type = {7, 7, 7, 7, 7, 7, 7, 7, 23, 7, 15, 15, 15, 15, 21, 16, 15, 15, 15, 15, 23, 15, 15, 15, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 16, 16, 16, 23, 16, 16, 16, 16, 16, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 21, 21, 21, 19, 19, 21, 21, 7, 23, 7, 7, 7, 7, 21, 15, 15, 15, 15, 15, 15, 15, 16, 16, 23, 16, 16, 16, 16, 16, 16, 16, 21, 6, 6, 6, 21, 6, 6, 6, 6, 1, 23, 1, 1, 1, 1, 1, 16, 15, 15, 15, 15, 15, 15, 3, 3, 3, 3, 3, 1, 1, 1, 21, 3, 3, 23, 21, 15, 15, 21, 7, 23, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 23, 7, 7, 7, 7, 7, 7, 7, 7, 23, 7, 7, 23, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 21, 21, 21, 23, 21, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16, 23, 6, 23, 6, 6, 6, 6, 23, 6, 6, 6, 6, 23, 6, 6, 6, 16, 7, 7, 7, 16, 16, 16, 16, 16, 21, 15, 15, 15, 15, 15, 23, 15, 15, 15, 15, 23, 7, 21, 21, 21, 23, 21, 21, 21, 23, 21, 21, 21, 21, 21, 6, 6, 23, 6, 6, 6, 21, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 23, 21, 21, 21, 16, 16, 16, 16, 16, 21, 21, 6, 6, 7, 7, 7, 7, 7, 23, 7, 7, 7, 23, 21, 23, 6, 23, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 12, 12, 12, 12, 12, 23, 12, 12, 12, 12, 12, 12, 12, 12, 12, 23, 12, 23, 12, 23, 12, 12, 21, 21, 21, 21, 12, 12, 12, 12, 12, 12, 12, 12, 12, 7, 23, 7, 7, 7, 7, 7, 7, 7, 21, 21, 21, 21, 21, 21, 23, 21, 21, 11, 11, 23, 11, 11, 23, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 21, 21, 21, 23, 21, 21, 21, 15, 15, 15, 21, 21, 6, 6, 6, 6, 6, 6, 6, 1, 1, 23, 1, 21, 21, 1, 1, 21, 21, 21, 21, 12, 12, 12, 12, 12, 12, 23, 12, 12, 6, 6, 1, 1, 0, 21, 0, 3, 3, 23, 3, 21, 21, 21, 21, 6, 23, 6, 6, 6, 6, 6, 21, 21, 21, 23, 23, 21, 21, 21, 21, 6, 21, 21, 21, 23, 23, 6, 23, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 23, 6, 7, 7, 7, 7, 15, 15, 15, 15, 15, 15, 15, 21, 23, 6, 6, 6, 23, 23, 6, 23, 7, 7, 15, 15, 15, 15, 15, 23, 15, 3, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 3, 3, 1, 0, 23, 0, 0, 1, 1, 23, 1, 23, 1, 1, 12, 12, 23, 7, 7, 7, 21, 21, 21, 21, 21, 1, 3, 3, 3, 7, 7, 23, 7, 7, 1, 1, 1, 1, 1, 1, 1, 1, 3, 3, 3, 23, 3, 3, 3, 3, 3, 3, 1, 7, 7, 7, 1, 1, 3, 3, 0, 0, 0, 1, 1, 1, 1, 7, 7, 7, 1, 1, 1, 1, 1, 1, 1, 23, 1, 23, 23, 1, 0, 23, 0, 0, 0, 1, 15, 15, 15, 15, 15, 15, 3, 3, 3, 3, 3, 3, 3, 23, 3, 23, 23, 3, 3, 3, 6, 6, 1, 1, 12, 12, 12, 12, 12, 12, 3, 3, 1, 1, 0, 1, 1, 23, 1, 23, 23, 23, 21, 0, 0, 23, 3, 0, 0, 0, 1, 23, 1, 1, 21, 21, 21, 11, 11, 11, 11, 21, 21, 23, 6, 6, 1, 7, 0, 0, 0, 1, 1, 1, 1, 1, 7, 7, 7, 21, 21, 6, 6, 6, 7, 7, 7, 7, 7, 1, 1};
	std::vector<int> unique_type = {0, 1, 3, 5, 12, 15, 22, 25};

	float maxPressure = 0.0f;

	int nStroke = 0, nVertex = 0;


	while (std::getline(file, line))
	{
		if (line.empty())
		{
			++nStroke;
			curves.push_back(std::move(curve));
			pressures.push_back(std::move(pressure));
			curve = Geom::Polyline{};
			pressure.clear();
			continue;
		}

		++nVertex;
		std::vector<float> values;
		for (auto value : views::split(line, ','))
		{
			std::string s(value.begin(), value.end());
			values.push_back(std::stof(s));
		}
		curve.push_back(values[0], values[1]);
		allPoints.push_back(values[0], values[1]);
		pressure.push_back(values[2]);
		if (values[2] >= maxPressure) maxPressure = values[2];
	}

	spdlog::info("Number of strokes: {}", nStroke);
	spdlog::info("Number of vertices: {}", nVertex);

// scale here!!!
	glm::vec2 boundSize = allPoints.BoundingBox()[1] - allPoints.BoundingBox()[0];
	auto& canvas = R.ctx().get<Canvas>();
	glm::vec2 factorXY = boundSize / canvas.Viewport.GetSize();
	float factor = 1.0f / glm::max(factorXY.x, factorXY.y);
	factor *= 0.8f;
	glm::vec2 mid = (allPoints.BoundingBox()[1] + allPoints.BoundingBox()[0]) / 2.0f;

	srand((unsigned) time(0));
	
	float R_value;
	float G_value;
	float B_value;
	float A_value;

	std::string stringpath = filePath.generic_string();
	std::string prefix = "./figure_csv/";
	int prefixLen = prefix.size();

	std::string saveName = stringpath.substr(prefixLen);

	std::vector<std::string> strokeData;
	std::vector<std::vector<std::string>> pngData;
	std::string pathData;

	std::vector<int> brush_stamp {1,13,16,32,38,41};
	std::vector<int> brush_mode {0,1};
	std::vector<float> brush_interval {0,0.1,0.2};
	std::vector<float> brush_noise {0,0.1,0.2,0.4,1};
	std::vector<float> brush_rotation {1,1.5,2};

	int brushNum1 = rand() % 16;
	int brushNum2 = rand() % 16;
	int brushNum3 = rand() % 16;
	int brushNum4 = rand() % 16;
	// int brushNum = rand() % ( brush_stamp.size() );

	// std::string nameTemp = saveName.substr(0,saveName.size() - 4);
	// std::string pngName = nameTemp + "_brush" + std::to_string(brushNum) + "_" + std::to_string(iterate);

	for (int i = 0; i < curves.size(); ++i)
	{

	// for random values
		std::random_device rd;
		std::mt19937 gen(rd());
		int randomInt;

	// scale here!!!
		auto& c = curves[i];
		c = c.Scale({factor, factor}, mid);
		c = c.Translate(-mid + canvas.Viewport.GetSize() / 2.0f);

		int NumPoints = 0;
		// std::cout << canvas.Viewport.GetSize()[0] << " " << canvas.Viewport.GetSize()[1] << std::endl;
		pathData = "";
		for(auto& cPoint : c){
			pathData += std::to_string(cPoint[0]) + ";" + std::to_string(cPoint[1]) + ";";
			NumPoints++;
		}

	// original thickness offset
		auto& offset = pressures[i];

		float maxPressureOnStroke = 0.0f;
		for (float& t : offset){
			if(t > maxPressureOnStroke){
				maxPressureOnStroke = t;
			}
		}

		std::cout << std::to_string(NumPoints) << std::endl;

		// for (float& t : offset) t = -(0.8f - (t/maxPressure) * (t/maxPressure)) * TargetThickness;
		// for (float& t : offset) t = -((t/maxPressure - 0.75) * (t/maxPressure - 0.75)) * targetThickness;

		
	// (my) no thickness offset
		// std::vector<float> noOffset(offset.size()); 
		// for(float& s: noOffset) s = 0.0f; 

		entt::entity strokeE = R.create();
		R.emplace<StrokeUsageFlags>(strokeE, StrokeUsageFlags::Final | StrokeUsageFlags::Arrange);
		R.ctx().get<StrokeContainer>().StrokeEs.push_back(strokeE);
		auto& stroke = R.emplace<Stroke>(strokeE);
		stroke.Position = c;
		
		// stroke.ThicknessOffset = noOffset;

	// random thickness
		// std::uniform_int_distribution<int> thicknessDis(0, 50);
		// randomInt = thicknessDis(gen);
		// float randomThickness = randomInt * 0.0001f + 0.0001f;
		
		
		// stroke.Thickness = randomThickness;

	// render random colors to strokes
		// R_value = (float)rand() / RAND_MAX;
		// G_value = (float)rand() / RAND_MAX;
		// B_value = (float)rand() / RAND_MAX;
		// A_value = (float)rand() / RAND_MAX;
		

		// stroke.BrushE = brushE;
		float pressureThreshold = 0.8;
		float orderThreshold = 0.6;

		int curvesSize = curves.size();

		// for(int j = 0; j < unique_type.size(); j++){
		// 	if(all_type[i] == unique_type[j]){
		// 		stroke.BrushE = R.ctx().get<BrushManager>().Brushes[unique_type[j] + 3];
		// 		stroke.Color = {0.78, std::clamp(1.0 - (0.14 * j),0.0,1.0), std::clamp(0.14 * j,0.0,1.0), std::clamp(1.0 - 0.08 * j,0.0,1.0)};
		// 	}
		// }
		
		// OpenSketch: presentation

		// if(all_type[i] <= 2 || all_type[i] == 24){
		// 	stroke.Color = ContourColor;
		// 	stroke.BrushE = R.ctx().get<BrushManager>().Brushes[ContourIndex];
		// 	for (float& t : offset) t = -(0.8f - (t/maxPressure) * (t/maxPressure)) * ContourThickness;
		// 	stroke.Thickness = ContourThickness;
		// }else if(all_type[i] == 22){
		// 	stroke.Color = HatchingColor;
		// 	stroke.BrushE = R.ctx().get<BrushManager>().Brushes[HatchingIndex];
		// 	for (float& t : offset) t = -(0.8f - (t/maxPressure) * (t/maxPressure)) * HatchingThickness;
		// 	stroke.Thickness = HatchingThickness;
		// }else if(all_type[i] == 25){
		// 	stroke.Color = HatchingOutlineColor;
		// 	stroke.BrushE = R.ctx().get<BrushManager>().Brushes[HatchingOutlineIndex];
		// 	for (float& t : offset) t = -(0.8f - (t/maxPressure) * (t/maxPressure)) * HatchingOutlineThickness;
		// 	stroke.Thickness = HatchingOutlineThickness;
		// }else{
		// 	stroke.Color = DetailColor;
		// 	stroke.BrushE = R.ctx().get<BrushManager>().Brushes[DetailIndex];
		// 	for (float& t : offset) t = -(0.8f - (t/maxPressure) * (t/maxPressure)) * DetailThickness;
		// 	stroke.Thickness = DetailThickness;
		// }

		// OpenSketch: concept

		// if(all_type[i] <= 2){
		// 	stroke.Color = ContourColor;
		// 	stroke.BrushE = R.ctx().get<BrushManager>().Brushes[ContourIndex];
		// 	for (float& t : offset) t = -(0.8f - (t/maxPressure) * (t/maxPressure)) * ContourThickness;
		// 	stroke.Thickness = ContourThickness;
		// }else if(all_type[i] >= 3 && all_type[i] <= 5){
		// 	stroke.Color = HatchingColor;
		// 	stroke.BrushE = R.ctx().get<BrushManager>().Brushes[HatchingIndex];
		// 	for (float& t : offset) t = -(0.8f - (t/maxPressure) * (t/maxPressure)) * HatchingThickness;
		// 	stroke.Thickness = HatchingThickness;
		// }else if(all_type[i] == 21){
		// 	stroke.Color = HatchingOutlineColor;
		// 	stroke.BrushE = R.ctx().get<BrushManager>().Brushes[HatchingOutlineIndex];
		// 	for (float& t : offset) t = -(0.8f - (t/maxPressure) * (t/maxPressure)) * HatchingOutlineThickness;
		// 	stroke.Thickness = HatchingOutlineThickness;
		// }else{
		// 	stroke.Color = DetailColor;
		// 	stroke.BrushE = R.ctx().get<BrushManager>().Brushes[DetailIndex];
		// 	for (float& t : offset) t = -(0.8f - (t/maxPressure) * (t/maxPressure)) * DetailThickness;
		// 	stroke.Thickness = DetailThickness;
		// }

		// normal

		stroke.Color = DetailColor;
		stroke.BrushE = R.ctx().get<BrushManager>().Brushes[DetailIndex];
		for (float& t : offset) t = -(0.8f - (t/maxPressure) * (t/maxPressure)) * DetailThickness;
		stroke.Thickness = DetailThickness;



		// if(NumPoints < PointsThreshold || i < OrderThreshold * curvesSize){
		// 	for (float& t : offset) t = -(0.8f - (t/maxPressure) * (t/maxPressure)) * ContourThickness;
			
		// 	stroke.BrushE = R.ctx().get<BrushManager>().Brushes[ContourIndex];
		// 	stroke.Color = ContourColor;
		// 	// stroke.Color = {0.16, 0.47, 0.71, ContourAlpha};
		// 	stroke.Thickness = ContourThickness; //targetThickness
		// }else{
		// 	for (float& t : offset) t = -(0.8f - (t/maxPressure) * (t/maxPressure)) * DetailThickness;
		// 	stroke.BrushE = R.ctx().get<BrushManager>().Brushes[DetailIndex];
		// 	stroke.Color = DetailColor;
		// 	// stroke.Color = {0.78, 0.14, 0.14, DetailAlpha};
		// 	stroke.Thickness = DetailThickness; //targetThickness
		// }

		// if(maxPressureOnStroke > PressureThreshold * maxPressure || i < OrderThreshold * curvesSize){
		// 	for (float& t : offset) t = -(0.8f - (t/maxPressure) * (t/maxPressure)) * ContourThickness;
			
		// 	stroke.BrushE = R.ctx().get<BrushManager>().Brushes[ContourIndex];
		// 	stroke.Color = ContourColor;
		// 	// stroke.Color = {0.16, 0.47, 0.71, ContourAlpha};
		// 	stroke.Thickness = ContourThickness; //targetThickness
		// }else{
		// 	for (float& t : offset) t = -(0.8f - (t/maxPressure) * (t/maxPressure)) * DetailThickness;
		// 	stroke.BrushE = R.ctx().get<BrushManager>().Brushes[DetailIndex];
		// 	stroke.Color = DetailColor;
		// 	// stroke.Color = {0.78, 0.14, 0.14, DetailAlpha};
		// 	stroke.Thickness = DetailThickness; //targetThickness
		// }

		stroke.ThicknessOffset = offset;
		// stroke.BrushE = R.ctx().get<BrushManager>().Brushes[6]; //render random brushes to strokes brushNum
		// if(brushNum < 109){
		// 	auto& brushT = R.get<Brush>(stroke.BrushE);

		// 	brushT.Stamp->NoiseFactor = noiseFactor;
		// 	brushT.Stamp->RotationRand = rotateRandom;
		// 	brushT.Stamp->StampIntervalRatio = intervalRatio;
		// 	brushT.Stamp->StampMode = StampBrushData::StampMode::EquiDistant;
		// }
		
		// if(i < curves.size()/3){

		// 	stroke.BrushE = R.ctx().get<BrushManager>().Brushes[9]; //render random brushes to strokes

		// 	auto& brushT = R.get<Brush>(stroke.BrushE);

		// 	brushT.Stamp->NoiseFactor = 0.0;
		// 	brushT.Stamp->RotationRand = 2.0;
		// 	brushT.Stamp->StampIntervalRatio = 0.06;
		// 	brushT.Stamp->StampMode = StampBrushData::StampMode::RatioDistant;

		// }else if (i < curves.size()/2){
		// 	// brushNum = rand() % ( R.ctx().get<BrushManager>().Brushes.size() );

		// 	stroke.BrushE = R.ctx().get<BrushManager>().Brushes[4]; //render random brushes to strokes

		// 	auto& brushT = R.get<Brush>(stroke.BrushE);

		// 	brushT.Stamp->NoiseFactor = 0;
		// 	brushT.Stamp->RotationRand = 1.14;
		// 	brushT.Stamp->StampIntervalRatio = 0.05;
		// 	brushT.Stamp->StampMode = StampBrushData::StampMode::RatioDistant;

		// 	// brushT.Stamp->NoiseFactor = 0.15;
		// 	// brushT.Stamp->RotationRand = 0.97;
		// 	// brushT.Stamp->StampIntervalRatio = 0.13;
		// 	// brushT.Stamp->StampMode = StampBrushData::StampMode::EquiDistant;

		// }
		// else if (i < curves.size()*3/4){
		// 	// stroke.Thickness = 0.001;
		// 	// brushNum = rand() % ( R.ctx().get<BrushManager>().Brushes.size() );
		// 	stroke.BrushE = R.ctx().get<BrushManager>().Brushes[0]; //render random brushes to strokes

		// 	auto& brushT = R.get<Brush>(stroke.BrushE);

		// 	brushT.Stamp->NoiseFactor = 0;
		// 	brushT.Stamp->RotationRand = 1.14;
		// 	brushT.Stamp->StampIntervalRatio = 0.2;
		// 	brushT.Stamp->StampMode = StampBrushData::StampMode::RatioDistant;

		// }
		// else{
		// 	// brushNum = rand() % ( R.ctx().get<BrushManager>().Brushes.size() );

		// 	stroke.BrushE = R.ctx().get<BrushManager>().Brushes[13]; //render random brushes to strokes

		// 	auto& brushT = R.get<Brush>(stroke.BrushE);

		// 	brushT.Stamp->NoiseFactor = 0.0;
		// 	brushT.Stamp->RotationRand = 1.2;
		// 	brushT.Stamp->StampIntervalRatio = 0.1;
		// 	brushT.Stamp->StampMode = StampBrushData::StampMode::EquiDistant;
		// }

		// stroke.Color = {R_value, G_value, B_value, A_value}; 

		// std::uniform_int_distribution<int> noiseDis(0, 100);
		// randomInt = noiseDis(gen);
		// float randomNoise = randomInt * 0.01f + 0.01f;

		// std::uniform_int_distribution<int> rotationDis(0, 199);
		// randomInt = rotationDis(gen);
		// float randomRotation = randomInt * 0.01f + 0.01f;

		// std::uniform_int_distribution<int> intervalDis(0, 50);
		// randomInt = intervalDis(gen);
		// float randomInterval = randomInt * 0.01f + 0.01f;

		// std::uniform_int_distribution<int> distantDis(0, 2);
		// int randomDistant = distantDis(gen);

		
		// if(brushT.Name == "Vanilla"){
		// 	strokeData = {};
		// 	strokeData.push_back(std::to_string(i));
		// 	strokeData.push_back(pathData); //path
		// 	strokeData.push_back(std::to_string(stroke.Thickness));
		// 	strokeData.push_back(std::to_string(stroke.Color[0]));
		// 	strokeData.push_back(std::to_string(stroke.Color[1]));
		// 	strokeData.push_back(std::to_string(stroke.Color[2]));
		// 	strokeData.push_back(std::to_string(stroke.Color[3]));
		// 	strokeData.push_back("Vanilla");

		// 	// std::cout << filePath << " stroke " << std::to_string(i) << " thickness: " << std::to_string(stroke.Thickness) 
		// 	// 			<< " R: " << stroke.Color[0] << " G: " << stroke.Color[1] << " B: " << stroke.Color[2] << " A: " << stroke.Color[3] 
		// 	// 			<< " brushName: " << brushT.Name << std::endl;
		// }else if(brushT.Name == "Airbrush"){
		// 	strokeData = {};
		// 	strokeData.push_back(std::to_string(i));
		// 	strokeData.push_back(pathData); //path
		// 	strokeData.push_back(std::to_string(stroke.Thickness));
		// 	strokeData.push_back(std::to_string(stroke.Color[0]));
		// 	strokeData.push_back(std::to_string(stroke.Color[1]));
		// 	strokeData.push_back(std::to_string(stroke.Color[2]));
		// 	strokeData.push_back(std::to_string(stroke.Color[3]));
		// 	strokeData.push_back("Airbrush");

		// 	// std::cout << filePath << " stroke " << std::to_string(i) << " thickness: " << std::to_string(stroke.Thickness) 
		// 	// 			<< " R: " << stroke.Color[0] << " G: " << stroke.Color[1] << " B: " << stroke.Color[2] << " A: " << stroke.Color[3] 
		// 	// 			<< " brushName: " << brushT.Name << std::endl;
		// }
		// else{
		// brushT.Stamp->NoiseFactor = randomNoise;
		// brushT.Stamp->RotationRand = randomRotation;
		// brushT.Stamp->StampIntervalRatio = randomInterval;
		// if(randomDistant == 0){
		// 	brushT.Stamp->StampMode = StampBrushData::StampMode::EquiDistant;
		// }else{
		// 	brushT.Stamp->StampMode = StampBrushData::StampMode::RatioDistant;
		// }

		// strokeData = {};
		// strokeData.push_back(pngName);
		// strokeData.push_back(std::to_string(stroke.Thickness));
		// strokeData.push_back(std::to_string(stroke.Color[0]));
		// strokeData.push_back(std::to_string(stroke.Color[1]));
		// strokeData.push_back(std::to_string(stroke.Color[2]));
		// strokeData.push_back(std::to_string(stroke.Color[3]));

		// std::string brushName = brushT.Name;
		// int stampNum = std::stoi(brushName.substr(5));
		// strokeData.push_back(std::to_string(stampNum));

		// strokeData.push_back(std::to_string(brushT.Stamp->NoiseFactor));
		// strokeData.push_back(std::to_string(brushT.Stamp->RotationRand));
		// strokeData.push_back(std::to_string(brushT.Stamp->StampIntervalRatio));
		// strokeData.push_back(std::to_string(brushT.Stamp->StampMode));
		

		// pngData.push_back(strokeData);
		strokeData.push_back(pathData);
		// std::cout << pathData << std::endl;

		stroke.UpdateBuffers();
		arm.AddOrUpdate(strokeE);
	}

	
	// std::string figurePrefix = "./figure_csv/";
	// std::string figureName = stringpath.substr(figurePrefix.size());
	
	// std::string figurePath = "figure-path/" + figureName;
	// std::cout << figurePath << std::endl;
	// std::ofstream saveFile("test.csv", std::ofstream::out | std::ofstream::app);
	// std::ofstream saveFile(figurePath);

	// for (const auto& row : strokeData)
    // {
    //         saveFile << row;
	// 		saveFile << std::endl;
    // }

	// saveFile.close();

	// std::vector<std::string> headline{"Stroke No.", "Path", "Thickness", "R value", "G value", "B value", "A value", "Brush type", "Stamp image" "Noise factor,", "Rotation randomness", "Interval", "Stamp mode"};
	// std::vector<std::string> headline{"PNG name", "Thickness", "Stamp image" "Noise factor,", "Rotation randomness", "Interval", "Stamp mode"};
	// for (const auto& line : headline){
	// 	saveFile << line << ",";
	// }
	// saveFile << std::endl;

	// for (const auto& row : pngData)
    // {
    //     for (const auto& cell : row)
    //     {
    //         saveFile << cell << ",";
    //     }
    //     saveFile << std::endl;
    // }
    // saveFile.close();

}

void Loader::SaveCsv(const std::filesystem::path& filePath)
{
	std::ofstream file(filePath);
	
}

void Loader::DrawUI()
{
	ImGui::Begin("Loader");

	// ImGui::DragInt("Brush Index", &BrushIndex, 1.0f, 0, 115);
	// ImGui::DragFloat("Thickness", &TargetThickness, 0.002f, 0.0f, 0.5f);
	// ImGui::DragFloat("Alpha", &TargetAlpha, 0.002f, 0.0f, 1.f);
	ImGui::DragFloat("pressureThresholod", &PressureThreshold, 0.01f, 0.0f, 1.0f);
	ImGui::DragFloat("orderThreshold", &OrderThreshold, 0.01f, 0.0f, 1.0f);
	ImGui::DragInt("Contour Index", &ContourIndex, 1.0f, 0, 115);
	// ImGui::DragInt("Occluded Index", &OccludedIndex, 1.0f, 0, 115);
	ImGui::DragInt("Hatching Index", &HatchingIndex, 1.0f, 0, 115);
	ImGui::DragInt("Hatching Outline Index", &HatchingOutlineIndex, 1.0f, 0, 115);
	ImGui::DragInt("Detail Index", &DetailIndex, 1.0f, 0, 115);

	ImGui::DragInt("pointsThreshold", &PointsThreshold, 1.0f, 0, 115);

	ImGui::DragFloat("Contour Thickness", &ContourThickness, 0.002f, 0.0f, 0.5f);
	// ImGui::DragFloat("Occluded Thickness", &OccludedThickness, 0.002f, 0.0f, 0.5f);
	ImGui::DragFloat("Hatching Thickness", &HatchingThickness, 0.002f, 0.0f, 0.5f);
	ImGui::DragFloat("Hatching Outline Thickness", &HatchingOutlineThickness, 0.002f, 0.0f, 0.5f);
	ImGui::DragFloat("Detail Thickness", &DetailThickness, 0.002f, 0.0f, 0.5f);

	ImGui::ColorPicker4("Conctour Color##0", glm::value_ptr(ContourColor), ImGuiColorEditFlags_DisplayRGB);
	// ImGui::ColorPicker4("Occluded Color##1", glm::value_ptr(OccludedColor), ImGuiColorEditFlags_DisplayRGB);
	ImGui::ColorPicker4("Hatching Color##1", glm::value_ptr(HatchingColor), ImGuiColorEditFlags_DisplayRGB);
	ImGui::ColorPicker4("Hatching Outline Color##2", glm::value_ptr(HatchingOutlineColor), ImGuiColorEditFlags_DisplayRGB);
	ImGui::ColorPicker4("Detail Color##3", glm::value_ptr(DetailColor), ImGuiColorEditFlags_DisplayRGB);
	ImGui::End();
}
