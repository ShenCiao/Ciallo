#include "pch.hpp"
#include "Loader.h"

#include <fstream>
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

void Loader::LoadCsv(const std::filesystem::path& filePath)
{
	// Warning: memory leak! (not trying to remove unused stroke)
	entt::entity drawingE = R.ctx().get<TimelineManager>().GetCurrentDrawing();
	if (drawingE == entt::null) return;
	R.get<StrokeContainer>(drawingE).StrokeEs.clear();
	auto& arm = R.get<ArrangementManager>(drawingE);
	arm.Arrangement.clear();
	arm.LogSpeed = true;

	std::ifstream file(filePath);
	std::string line;
	file.exceptions(std::ifstream::badbit);

	Geom::Polyline curve, allPoints;
	std::vector<Geom::Polyline> curves;
	std::vector<std::vector<float>> pressures; // pen pressures get from gpencil
	std::vector<float> pressure;
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

	glm::vec2 boundSize = allPoints.BoundingBox()[1] - allPoints.BoundingBox()[0];
	auto& canvas = R.ctx().get<Canvas>();
	glm::vec2 factorXY = boundSize / canvas.Viewport.GetSize();
	float factor = 1.0f / glm::max(factorXY.x, factorXY.y);
	factor *= 0.95f;
	glm::vec2 mid = (allPoints.BoundingBox()[1] + allPoints.BoundingBox()[0]) / 2.0f;
	for (int i = 0; i < curves.size(); ++i)
	{
		auto& c = curves[i];
		c = c.Scale({factor, factor}, mid);
		c = c.Translate(-mid + canvas.Viewport.GetSize() / 2.0f);
		auto& offset = pressures[i];
		for (float& t : offset) t = - (1.0f - t / maxPressure) * TargetRadius;

		entt::entity strokeE = R.create();
		R.emplace<StrokeUsageFlags>(strokeE, StrokeUsageFlags::Final | StrokeUsageFlags::Arrange);
		R.get<StrokeContainer>(drawingE).StrokeEs.push_back(strokeE);

		auto& stroke = R.emplace<Stroke>(strokeE);
		stroke.Position = c;
		stroke.Color = StrokeColor;
		stroke.RadiusOffset = offset;
		stroke.Radius = TargetRadius;

		// I intented to use create a event system, but I'm lazy.
		stroke.BrushE = R.ctx().get<BrushManager>().Brushes[2];
		stroke.UpdateBuffers();

		//{
		//	entt::entity strokeE = R.create();
		//	R.emplace<StrokeUsageFlags>(strokeE, StrokeUsageFlags::Final);
		//	R.get<StrokeContainer>(drawingE).StrokeEs.push_back(strokeE);

		//	auto& stroke = R.emplace<Stroke>(strokeE);
		//	stroke.Position = c;
		//	stroke.RadiusOffset = std::vector<float>{0.0};
		//	stroke.Radius = TargetRadius/25.0;
		//	stroke.Color = { 82.f/255, 125.f/255, 255.f/255, 1.0f };

		//	// I intented to use create a event system, but I'm lazy.
		//	stroke.BrushE = R.ctx().get<BrushManager>().Brushes[0];
		//	stroke.UpdateBuffers();
		//}

		arm.AddOrUpdate(strokeE);
	}
}

void Loader::SaveCsv(const std::filesystem::path& filePath)
{
	std::ofstream file(filePath);
	
}

void Loader::LoadAnimation(const std::filesystem::path& filePath)
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

	for (const auto& entry : std::filesystem::directory_iterator(filePath)) {
		std::ifstream file(entry.path());
		file.exceptions(std::ifstream::badbit);

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
	}

	spdlog::info("#strokes: {}", curves.size());
	spdlog::info("#vertices: {}", allPoints.size());
	
	glm::vec2 boundSize = allPoints.BoundingBox()[1] - allPoints.BoundingBox()[0];
	auto& canvas = R.ctx().get<Canvas>();
	glm::vec2 factorXY = boundSize / canvas.Viewport.GetSize();
	float scaleFactor = 1.0f / glm::max(factorXY.x, factorXY.y);
	scaleFactor *= 0.95f;
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
			pressure.push_back(values[2]);
		}

		float minRadius = 0.0005f;
		for (int i = 0; i < curves.size(); ++i)
		{
			auto& c = curves[i];
			c = c.Scale({ scaleFactor, scaleFactor }, pivot);
			c = c.Translate(translate);
			auto& offset = pressures[i];
			for (float& t : offset) t = glm::pow(t / maxPressure, 1.5) * TargetRadius;

			entt::entity strokeE = R.create();
			R.emplace<StrokeUsageFlags>(strokeE, StrokeUsageFlags::Final | StrokeUsageFlags::Arrange);
			R.get<StrokeContainer>(drawingE).StrokeEs.push_back(strokeE);
			auto& stroke = R.emplace<Stroke>(strokeE);
			stroke.Position = c;
			stroke.Color = StrokeColor;
			stroke.RadiusOffset = offset;
			stroke.Radius = minRadius;

			// I intented to use create a event system, but I'm lazy.
			stroke.BrushE = R.ctx().get<BrushManager>().Brushes[0];
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
