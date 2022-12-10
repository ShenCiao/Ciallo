#include "pch.hpp"
#include "BrushManager.h"

#include <glm/gtc/constants.hpp>
#include "TextureManager.h"

void BrushManager::RenderPreview()
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
	s.Position = position;
	s.Thickness = thickness;
	s.ThicknessOffset = thicknessOffset;
	s.OnChanged();

	int width = static_cast<int>(1024 * 2 * gr);
	int height = 1024;
	glm::mat4 mvp = glm::ortho(-2.0f * gr, 2.0f * gr, -1.0f, 1.0f);

	glEnable(GL_BLEND);
	glBlendFuncSeparate(GL_SRC_ALPHA, GL_ONE_MINUS_SRC_ALPHA, GL_ONE, GL_ONE_MINUS_SRC_ALPHA);
	glDisable(GL_STENCIL_TEST);
	glDisable(GL_DEPTH_TEST);
	for (auto& b : Brushes)
	{
		s.Brush = b.get();
		b->PreviewTexture = RenderableTexture(width, height, 0);
		b->PreviewTexture.BindFramebuffer();
		glClearColor(0.0f, 0.0f, 0.0f, 0.0f);
		glClear(GL_COLOR_BUFFER_BIT);
		b->Use();
		glUniformMatrix4fv(0, 1, false, glm::value_ptr(mvp));
		s.SetUniforms();
		s.DrawCall();
		b->PreviewTexture.CopyMS();
	}
}

void BrushManager::Draw()
{
	ImGui::Begin("Toolbox");
	if (ImGui::BeginPopup("Brushes"))
	{
		for (auto& b : Brushes)
			ImGui::Image(reinterpret_cast<ImTextureID>(b->PreviewTexture.ColorTexture),
			             {256 * 2 * glm::golden_ratio<float>(), 256});
		ImGui::EndPopup();
	}
	ImGui::End();
}

void BrushManager::OutputPreview()
{
	for(auto& b : Brushes)
	{
		std::string prefix = "brush_";
		TextureManager::SaveTexture(b->PreviewTexture.ColorTexture, prefix + b->Name);
	}
}
