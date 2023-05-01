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
	const int segments = 16;
	Geom::Polyline position;
	const float thickness = 0.33f;
	std::vector<float> thicknessOffset;
	for (int i = 0; i <= segments; ++i)
	{
		float a = static_cast<float>(i) / segments;
		float x = glm::mix(-pi, pi, a);
		float y = 1.0f / gr * glm::sin(x);
		float t = (glm::cos(x / 2.0f) - 1.0f) * thickness;
		position.push_back(x, y);
		thicknessOffset.push_back(t);
	}
	PreviewStroke.Position = position;
	PreviewStroke.Thickness = thickness;
	PreviewStroke.ThicknessOffset = thicknessOffset;
	PreviewStroke.UpdateBuffers();
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
	const int height = 256, width = static_cast<int>(height * 2 * gr);

	auto& brush = R.get<Brush>(brushE);
	if(brush.Stamp)
	{
		PreviewStroke.UpdateDistanceBuffer(brush.Stamp->StampMode);
	}
	brush.PreviewTexture = RenderableTexture(width, height, 0);
	brush.PreviewTexture.BindFramebuffer();
	glClearColor(1.0f, 1.0f, 1.0f, 1.0f);
	glClear(GL_COLOR_BUFFER_BIT);
	brush.Use();
	brush.SetUniforms();
	PreviewStroke.SetUniforms();
	PreviewStroke.LineDrawCall();
	brush.PreviewTexture.CopyMS();
	glBindFramebuffer(GL_FRAMEBUFFER, 0);
}

void BrushManager::DrawUI()
{
	ImVec2 center = ImGui::GetMainViewport()->GetCenter();
	ImGui::SetNextWindowPos(center, ImGuiCond_Appearing, ImVec2(0.5f, 0.5f));

	ImGui::Begin("Toolbox");
	if (ImGui::BeginPopupModal("BrushEditor", nullptr, ImGuiWindowFlags_AlwaysAutoResize))
	{
		ImGui::BeginChild("left pane", ImVec2(150, 600), true);
		for (entt::entity e : Brushes)
		{
			auto& brush = R.get<Brush>(e);
			if (ImGui::Selectable(brush.Name.c_str(), e == EditorActiveBrushE))
				EditorActiveBrushE = e;
		}
		ImGui::EndChild();

		ImGui::SameLine();

		ImGui::BeginGroup();
		auto& brush = R.get<Brush>(EditorActiveBrushE);
		SetContext();
		RenderPreview(EditorActiveBrushE);
		const int w = 600;
		ImGui::BeginChild("right panel", ImVec2(w, -ImGui::GetFrameHeightWithSpacing()));
		ImGui::Image(reinterpret_cast<ImTextureID>(brush.PreviewTexture.ColorTexture),
		             {w, w / 2.0f / glm::golden_ratio<float>()});
		ImGui::ColorEdit4("demo color", glm::value_ptr(PreviewStroke.Color), ImGuiColorEditFlags_DisplayRGB);
		ImGui::Separator();
		brush.DrawProperties();
		ImGui::EndChild();

		if (ImGui::Button("Use"))
		{
			ImGui::CloseCurrentPopup();
			*TargetBrushE = EditorActiveBrushE;
			TargetBrushE = nullptr;
			EditorActiveBrushE = entt::null;
		}
		ImGui::SameLine();
		if (ImGui::Button("Cancel"))
		{
			ImGui::CloseCurrentPopup();
			TargetBrushE = nullptr;
			EditorActiveBrushE = entt::null;
		}
		ImGui::SameLine();
		if (ImGui::Button("Export Demo"))
		{
			ExportDemo();
		}
		ImGui::EndGroup();
		ImGui::EndPopup();
	}
	ImGui::End();
}

void BrushManager::OpenBrushEditor(entt::entity* brushE)
{
	EditorActiveBrushE = *brushE;
	TargetBrushE = brushE;

	ImGui::OpenPopup("BrushEditor", ImGuiPopupFlags_AnyPopup);
}

void BrushManager::ExportDemo()
{
	auto& brush = R.get<Brush>(EditorActiveBrushE);
	std::string prefix = "brush_";
	TextureManager::SaveTexture(brush.PreviewTexture.ColorTexture, prefix + brush.Name);
}
