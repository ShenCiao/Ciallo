#include "pch.hpp"
#include "AirBrushData.h"

#include "implot.h"

AirBrushData::AirBrushData()
{
}

AirBrushData::AirBrushData(const AirBrushData& other): Curve(other.Curve)
{
	UpdateGradient();
}

AirBrushData::AirBrushData(AirBrushData&& other) noexcept: Gradient(other.Gradient),
                                                           Curve(std::move(other.Curve))
{
	other.Gradient = 0;
}

AirBrushData& AirBrushData::operator=(AirBrushData other)
{
	using std::swap;
	swap(*this, other);
	return *this;
}

AirBrushData::~AirBrushData()
{
	glDeleteTextures(1, &Gradient);
}

void AirBrushData::UpdateGradient()
{
	glDeleteTextures(1, &Gradient);
	glCreateTextures(GL_TEXTURE_1D, 1, &Gradient);
	glTextureParameteri(Gradient, GL_TEXTURE_MIN_FILTER, GL_LINEAR);
	glTextureParameteri(Gradient, GL_TEXTURE_MAG_FILTER, GL_LINEAR);
	glTextureParameteri(Gradient, GL_TEXTURE_WRAP_S, GL_MIRRORED_REPEAT);
	glTextureParameteri(Gradient, GL_TEXTURE_WRAP_T, GL_MIRRORED_REPEAT);
	glTextureStorage1D(Gradient, 1, GL_RGBA32F, SampleCount);

	std::vector<float> values(SampleCount);
	for (int i = 0; i < SampleCount; ++i)
	{
		float x = static_cast<float>(i) / (SampleCount - 1);
		float t = Curve.FindT(x);
		if (t == std::numeric_limits<float>::min())
			throw std::runtime_error("Fail to find y value");
		values[i] = Curve(t).y;
	}
	glTextureSubImage1D(Gradient, 0, 0, SampleCount, GL_RED, GL_FLOAT, values.data());
}

void AirBrushData::SetUniforms()
{
	glBindTexture(GL_TEXTURE_1D, Gradient);
}

void AirBrushData::DrawProperties()
{
	ImGui::TextUnformatted("Alpha gradient");
	ImPlotAxisFlags axFlags = ImPlotAxisFlags_NoTickLabels | ImPlotAxisFlags_NoTickMarks | ImPlotAxisFlags_Lock;
	if (ImPlot::BeginPlot("Alpha Gradient##Bezier", ImVec2(-1, 0), ImPlotFlags_CanvasOnly))
	{
		// This stupid thing only accept double.
		ImPlot::SetupAxes(0, 0, axFlags, axFlags);
		ImPlot::SetupAxesLimits(0, 1, 0, 1);
		auto& cps = Curve.ControlPoints;
		ImPlotPoint P[] = {
			{cps[0].x, cps[0].y}, {cps[1].x, cps[1].y}, {cps[2].x, cps[2].y}, {cps[3].x, cps[3].y}
		};
		auto pFlags = ImPlotDragToolFlags_Delayed;
		ImPlot::DragPoint(1, &P[1].x, &P[1].y, ImVec4(1, 0.5f, 1, 1), 4, pFlags);
		ImPlot::DragPoint(2, &P[2].x, &P[2].y, ImVec4(0, 0.5f, 1, 1), 4, pFlags);

		const int n = 32;
		ImPlotPoint B[n];
		for (int i = 0; i < n; ++i)
		{
			double t = static_cast<double>(i) / n;
			double u = 1 - t;
			double w1 = u * u * u;
			double w2 = 3 * u * u * t;
			double w3 = 3 * u * t * t;
			double w4 = t * t * t;
			B[i] = ImPlotPoint(w1 * P[0].x + w2 * P[1].x + w3 * P[2].x + w4 * P[3].x,
			                   w1 * P[0].y + w2 * P[1].y + w3 * P[2].y + w4 * P[3].y);
		}

		
		for(int i = 0; i < 4; ++i)
		{
			glm::vec2 p = {P[i].x, P[i].y};
			cps[i] = glm::clamp(p, {0.0f, 0.0f}, {1.0f, 1.0f});
		}
		Curve.EvalLUT();

		ImPlot::SetNextLineStyle(ImVec4(1, 0.5f, 1, 1));
		ImPlot::PlotLine("##h1", &P[0].x, &P[0].y, 2, 0, 0, sizeof(ImPlotPoint));
		ImPlot::SetNextLineStyle(ImVec4(0, 0.5f, 1, 1));
		ImPlot::PlotLine("##h2", &P[2].x, &P[2].y, 2, 0, 0, sizeof(ImPlotPoint));
		ImPlot::SetNextLineStyle(ImVec4(0, 0.9f, 0, 1), 2);
		ImPlot::PlotLine("##bez", &B[0].x, &B[0].y, n, 0, 0, sizeof(ImPlotPoint));

		ImPlot::EndPlot();
	}
}

void swap(AirBrushData& lhs, AirBrushData& rhs) noexcept
{
	using std::swap;
	swap(lhs.Gradient, rhs.Gradient);
	swap(lhs.Curve, rhs.Curve);
}
