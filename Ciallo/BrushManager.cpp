#include "pch.hpp"
#include "BrushManager.h"

#include <glm/gtc/constants.hpp>
#include "TextureManager.h"
#include "Brush.h"
#include "Viewport.h"

BrushManager::BrushManager()
{
	GenPreviewStroke();
	auto gr = glm::golden_ratio<float>();
	PreviewPort = {{-2.0f * gr, -1.0f}, {2.0f * gr, 1.0f}};
	PreviewPort.UploadMVP();
}

void BrushManager::GenPreviewStroke()
{
	auto gr = glm::golden_ratio<float>();
	auto pi = glm::pi<float>();
	const int segments = 128;
	Geom::Polyline position;
	float thickness = 0.33f;
	std::vector<float> thicknessOffset;
	for (int i = 0; i <= segments; ++i)
	{
		float a = static_cast<float>(i) / segments;
		float x = glm::mix(-pi, pi, a);
		float y = 1.0f / gr * glm::sin(x);
		float t = glm::mix(0.0f, -thickness, glm::abs(2.0f * a - 1.0f));
		position.push_back(x, y);
		thicknessOffset.push_back(t);
	}
	PreviewStroke.Position = position;
	PreviewStroke.Thickness = thickness;
	PreviewStroke.ThicknessOffset = thicknessOffset;
	PreviewStroke.OnChanged();
}

void BrushManager::RenderAllPreview()
{
	SetContext();
	for (entt::entity brushE : Brushes)
	{
		RenderPreview(brushE);
	}
	glBindFramebuffer(GL_FRAMEBUFFER, 0);
}

void BrushManager::SetContext() const
{
	PreviewPort.BindMVPBuffer();
	glEnable(GL_BLEND);
	glBlendFuncSeparate(GL_SRC_ALPHA, GL_ONE_MINUS_SRC_ALPHA, GL_ONE, GL_ONE_MINUS_SRC_ALPHA);
	glDisable(GL_STENCIL_TEST);
	glDisable(GL_DEPTH_TEST);
}

void BrushManager::RenderPreview(entt::entity brushE)
{
	auto gr = glm::golden_ratio<float>();
	const int height = 1024, width = static_cast<int>(height * 2 * gr);
	PreviewStroke.Brush = brushE;

	auto& brush = R.get<Brush>(brushE);
	brush.PreviewTexture = RenderableTexture(width, height, 0);
	brush.PreviewTexture.BindFramebuffer();
	glClearColor(1.0f, 1.0f, 1.0f, 1.0f);
	glClear(GL_COLOR_BUFFER_BIT);
	brush.Use();
	brush.SetUniform();
	PreviewStroke.SetUniform();
	PreviewStroke.DrawCall();
	brush.PreviewTexture.CopyMS();
}

void BrushManager::DrawUI()
{
	ImGui::Begin("Toolbox");
	if (ImGui::BeginPopup("Brushes"))
	{
		for (entt::entity bE : Brushes)
		{
			auto& brush = R.get<Brush>(bE);
			ImGui::Image(reinterpret_cast<ImTextureID>(brush.PreviewTexture.ColorTexture),
				{ 256 * 2 * glm::golden_ratio<float>(), 256 });
		}
			
		ImGui::EndPopup();
	}
	ImGui::End();
}

void BrushManager::OutputPreview()
{
	for(auto& bE : Brushes)
	{
		auto& brush = R.get<Brush>(bE);
		std::string prefix = "brush_";
		TextureManager::SaveTexture(brush.PreviewTexture.ColorTexture, prefix + brush.Name);
	}
}
