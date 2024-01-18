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
#include "TimelineManager.h"
#include "TempLayers.h"
#include "Toolbox.h"
#include "Brush.h"
#include "InnerBrush.h"
#include "SelectionManager.h"
#include "EyedropperInfo.h"

void Loader::LoadCsv(const std::filesystem::path& filePath, float targetRadius, float intervalRatio)
{
	// Warning: memory leak! (not trying to remove unused stroke)
	entt::entity drawingE = R.ctx().get<TimelineManager>().GetCurrentDrawing();
	if (drawingE == entt::null) return;
	R.get<StrokeContainer>(drawingE).StrokeEs.clear();
	auto& arm = R.get<ArrangementManager>(drawingE);
	arm.Arrangement.clear();
	arm.LogSpeed = true;
	// spdlog::info("Arrangement Time: {}", arm.arrangementTime);
	

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

	// spdlog::info("Number of strokes: {}", nStroke);
	// spdlog::info("Number of vertices: {}", nVertex);

// scale here!!!
	glm::vec2 boundSize = allPoints.BoundingBox()[1] - allPoints.BoundingBox()[0];
	auto& canvas = R.ctx().get<Canvas>();
	glm::vec2 factorXY = boundSize / canvas.Viewport.GetSize();
	float factor = 1.0f / glm::max(factorXY.x, factorXY.y);
	factor *= 1.0f;
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
		// std::random_device rd;
		// std::mt19937 gen(rd());
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
		for (float& t : offset) t = - (1.0f - t / maxPressure) * targetRadius;

		entt::entity strokeE = R.create();
		R.emplace<StrokeUsageFlags>(strokeE, StrokeUsageFlags::Final | StrokeUsageFlags::Arrange);
		R.get<StrokeContainer>(drawingE).StrokeEs.push_back(strokeE);

		auto& stroke = R.emplace<Stroke>(strokeE);
		stroke.Position = c;
		stroke.RadiusOffset = offset;
		stroke.Radius = targetRadius;

		// I intented to use create a event system, but I'm lazy.
		stroke.BrushE = R.ctx().get<BrushManager>().Brushes[2];

		auto& brushT = R.get<Brush>(stroke.BrushE);

		brushT.Stamp->NoiseFactor = 0.0f;
		brushT.Stamp->RotationRand = 0.0f;
		brushT.Stamp->StampIntervalRatio = 1 / intervalRatio;
		brushT.Stamp->StampMode = StampBrushData::StampMode::EquiDistant;

		stroke.UpdateBuffers();
		//{
		//	entt::entity strokeE = R.create();
		//	R.emplace<StrokeUsageFlags>(strokeE, StrokeUsageFlags::Final);
		//	R.get<StrokeContainer>(drawingE).StrokeEs.push_back(strokeE);

		//	auto& stroke = R.emplace<Stroke>(strokeE);
		//	stroke.Position = c;
		//	stroke.RadiusOffset = std::vector<float>{0.0};
		//	stroke.Radius = targetRadius/25.0;
		//	stroke.Color = { 82.f/255, 125.f/255, 255.f/255, 1.0f };

		//	// I intented to use create a event system, but I'm lazy.
		//	stroke.BrushE = R.ctx().get<BrushManager>().Brushes[0];
		//	stroke.UpdateBuffers();
		//}

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

void Loader::LoadAnimation(const std::filesystem::path& filePath, float targetRadius)
{
	// Get transformation parameters
	const auto& entry = *std::filesystem::directory_iterator(filePath)++;
	std::ifstream file(entry.path());
	file.exceptions(std::ifstream::badbit);

	Geom::Polyline curve, allPoints;
	std::vector<Geom::Polyline> curves;
	std::vector<std::vector<float>> pressures;
	std::vector<float> pressure;
	float maxPressure = 0.0f;

	std::string line;
	while (std::getline(file, line))
	{
		if (line.empty())
		{
			curves.push_back(std::move(curve));
			pressures.push_back(std::move(pressure));
			curve = Geom::Polyline{};
			pressure.clear();
			continue;
		}

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

	glm::vec2 boundSize = allPoints.BoundingBox()[1] - allPoints.BoundingBox()[0];
	auto& canvas = R.ctx().get<Canvas>();
	glm::vec2 factorXY = boundSize / canvas.Viewport.GetSize();
	float scaleFactor = 1.0f / glm::max(factorXY.x, factorXY.y);
	scaleFactor *= 0.8f;
	glm::vec2 mid = (allPoints.BoundingBox()[1] + allPoints.BoundingBox()[0]) / 2.0f;

	// Transformation paramers
	glm::vec2 scale = { scaleFactor, scaleFactor };
	glm::vec2 pivot = mid;
	glm::vec2 translate = -mid + canvas.Viewport.GetSize() / 2.0f;

	// Import files
	R.ctx().get<TimelineManager>().Clear();

	for (const auto& entry : std::filesystem::directory_iterator(filePath))
	{
		curves.clear();
		pressures.clear();
		if (entry.path().extension() != ".csv")
			continue;
		std::ifstream file(entry.path());
		// filename without extension is the frame number
		auto frameNumberString = entry.path().filename().replace_extension().string();
		int frameNumber = std::stoi(frameNumberString);
		entt::entity drawingE = R.ctx().get<TimelineManager>().GenKeyFrame(frameNumber);
		auto& arm = R.get<ArrangementManager>(drawingE);

		while (std::getline(file, line))
		{
			if (line.empty())
			{
				curves.push_back(std::move(curve));
				pressures.push_back(std::move(pressure));
				curve = Geom::Polyline{};
				pressure.clear();
				continue;
			}

			std::vector<float> values;
			for (auto value : views::split(line, ','))
			{
				std::string s(value.begin(), value.end());
				values.push_back(std::stof(s));
			}
			curve.push_back(values[0], values[1]);
			pressure.push_back(values[2]);
		}

		float minRadius = 0.0005f;
		for (int i = 0; i < curves.size(); ++i)
		{
			auto& c = curves[i];
			c = c.Scale({ scaleFactor, scaleFactor }, pivot);
			c = c.Translate(translate);
			auto& offset = pressures[i];
			for (float& t : offset) t = glm::pow(t / maxPressure, 1.5) * targetRadius;

			entt::entity strokeE = R.create();
			R.emplace<StrokeUsageFlags>(strokeE, StrokeUsageFlags::Final | StrokeUsageFlags::Arrange);
			R.get<StrokeContainer>(drawingE).StrokeEs.push_back(strokeE);
			auto& stroke = R.emplace<Stroke>(strokeE);
			stroke.Position = c;
			stroke.RadiusOffset = offset;
			stroke.Radius = minRadius;

			// I intented to use create a event system, but I'm lazy.
			stroke.BrushE = R.ctx().get<BrushManager>().Brushes[1];
			stroke.UpdateBuffers();

			arm.AddOrUpdate(strokeE);
		}
	}
}

void Loader::LoadProject(const std::filesystem::path& filePath)
{
	entt::registry newR{};
	{
		auto storage = std::ifstream{ filePath, std::ios::binary };
		cereal::BinaryInputArchive archive{ storage };
		entt::snapshot_loader loader{ newR };
		loader.get<entt::entity>(archive)
			.get<Stroke>(archive)
			.get<StrokeUsageFlags>(archive)
			.get<StrokeContainer>(archive)
			.get<Brush>(archive);

		archive(newR.ctx().emplace<BrushManager>());
		archive(newR.ctx().emplace<TimelineManager>());
	}
	
	R = std::move(newR);
	
	auto& canvas = R.ctx().emplace<Canvas>();
	R.ctx().emplace<TempLayers>(canvas.GetSizePixel());
	R.ctx().emplace<Toolbox>();
	R.ctx().emplace<OverlayContainer>();
	R.ctx().emplace<InnerBrush>();
	R.ctx().emplace<SelectionManager>();
	R.ctx().emplace<EyedropperInfo>();

	auto view = R.view<StrokeContainer>();
	for (entt::entity drawingE : view) {
		auto& arm = R.emplace<ArrangementManager>(drawingE);
		auto& strokeEs = R.get<StrokeContainer>(drawingE).StrokeEs;
		for (entt::entity e : strokeEs) {
			auto& stroke = R.get<Stroke>(e);
			stroke.UpdateBuffers();
			auto usage = R.get<StrokeUsageFlags>(e);
			if (!!(usage & StrokeUsageFlags::Arrange))
				arm.AddOrUpdate(e);
			if (!!(usage & StrokeUsageFlags::Zone))
				arm.AddOrUpdateQuery(e);
			usage = usage | StrokeUsageFlags::Line;
		}
	}
}

void Loader::SaveProject(const std::filesystem::path& filePath)
{
	std::ofstream storage{ filePath, std::ios::binary };
	cereal::BinaryOutputArchive archive{ storage };

	entt::snapshot{ R }.get<entt::entity>(archive)
		.get<Stroke>(archive)
		.get<StrokeUsageFlags>(archive)
		.get<StrokeContainer>(archive)
		.get<Brush>(archive);

	archive(R.ctx().get<BrushManager>());
	archive(R.ctx().get<TimelineManager>());
}