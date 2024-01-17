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



int test() {
	const std::filesystem::path& filePath = "./models/grid.csv";
	float targetThickness = 0.3f;
	//// Warning: memory leak! (not trying to remove unused stroke)
	//R.ctx().get<StrokeContainer>().StrokeEs.clear();
	//auto& arm = R.ctx().get<ArrangementManager>();
	//arm.Arrangement.clear();
	//arm.LogSpeed = true;

	std::ifstream file(filePath);
	std::string line;
	file.exceptions(std::ifstream::badbit);

	Geom::Polyline curve, allPoints;
	std::vector<Geom::Polyline> curves;
	std::vector<std::vector<float>> pressures; // pen pressures get from gpencil
	std::vector<float> pressure;
	float maxPressure = 0.0f;

	int nStroke = 0, nVertex = 0;
	std::cout << "ok here" << std::endl;

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

		std::cout << "line: " << line << std::endl;

		++nVertex;
		std::vector<float> values;
		for (auto value : views::split(line, ','))
		{
			std::string s(value.begin(), value.end());
			values.push_back(std::stof(s));	//string to float
		}
		curve.push_back(values[0], values[1]);
		allPoints.push_back(values[0], values[1]);
		pressure.push_back(values[2]);
		if (values[2] >= maxPressure) maxPressure = values[2];
	}

	//std::vector<std::vector<float>> data = { {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, {7.0, 8.0, 9.0} };

	// Output each vector in the data
	for (const auto& vec : pressures) {
		std::cout << "Vector: ";
		for (const auto& val : vec) {
			std::cout << val << " ";
		}
		std::cout << std::endl;
	}


	spdlog::info("Number of strokes: {}", nStroke);
	spdlog::info("Number of vertices: {}", nVertex);

	glm::vec2 boundSize = allPoints.BoundingBox()[1] - allPoints.BoundingBox()[0];
	std::cout << boundSize[0] << std::endl;
	//auto& canvas = R.ctx().get<Canvas>();
	//glm::vec2 factorXY = boundSize / canvas.Viewport.GetSize();
	//float factor = 1.0f / glm::max(factorXY.x, factorXY.y);
	//factor *= 0.8f;
	//glm::vec2 mid = (allPoints.BoundingBox()[1] + allPoints.BoundingBox()[0]) / 2.0f;
	//for (int i = 0; i < curves.size(); ++i)
	//{
	//	auto& c = curves[i];
	//	c = c.Scale({ factor, factor }, mid);
	//	c = c.Translate(-mid + canvas.Viewport.GetSize() / 2.0f);
	//	auto& offset = pressures[i];
	//	for (float& t : offset) t = -(1.0f - t / maxPressure) * targetThickness;

	//	entt::entity strokeE = R.create();
	//	R.emplace<StrokeUsageFlags>(strokeE, StrokeUsageFlags::Final | StrokeUsageFlags::Arrange);
	//	R.ctx().get<StrokeContainer>().StrokeEs.push_back(strokeE);
	//	auto& stroke = R.emplace<Stroke>(strokeE);
	//	stroke.Position = c;
	//	stroke.ThicknessOffset = offset;
	//	stroke.Thickness = targetThickness;

	//	stroke.BrushE = R.ctx().get<BrushManager>().Brushes[2];
	//	stroke.UpdateBuffers();

	//	arm.AddOrUpdate(strokeE);
	//}
	return 0;
}




