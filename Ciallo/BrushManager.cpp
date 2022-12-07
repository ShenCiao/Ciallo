#include "pch.hpp"
#include "BrushManager.h"

#include <glm/gtc/constants.hpp>

#include "Stroke.h"

void BrushManager::RenderPreview()
{
	Stroke s;
	auto gr = glm::golden_ratio<float>();
	auto pi = glm::pi<float>();
	const int segments = 128;
	Geom::Polyline position;
	std::vector<float> thickness;
	for (int i = 0; i <= segments; ++i)
	{
		float a = static_cast<float>(i) / segments;
		float x = glm::mix(-pi, pi, a);
		float y = 0.25f * glm::sin(x);
		float t = glm::mix(0.33f, 0.0f, glm::abs(2.0f * a - 1.0f));
		position.push_back(x, y);
		thickness.push_back(t);
	}
	s.Position = position;
	s.Thickness = thickness;
	s.OnChanged();

	int width = static_cast<int>(1024 * 2 * gr);
	int height = 1024;
	glm::mat4 mvp = glm::ortho(-2.0f * gr, 2.0f * gr, -1.0f, 1.0f);

	glEnable(GL_BLEND);
	glDisable(GL_STENCIL_TEST);
	glDisable(GL_DEPTH_TEST);
	glViewport(0, 0, width, height);
	for (auto& b : Brushes)
	{
		b->PreviewTexture = RenderableTexture(width, height, 0);
		b->PreviewTexture.BindFramebuffer();
		glClearColor(1.0f, 1.0f, 1.0f, 1.0f);
		glClear(GL_COLOR_BUFFER_BIT);
		b->Use();
		glUniformMatrix4fv(0, 1, false, glm::value_ptr(mvp));
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
				{ 256 * 2* glm::golden_ratio<float>(), 256 });
		ImGui::EndPopup();
	}
	ImGui::End();
}
