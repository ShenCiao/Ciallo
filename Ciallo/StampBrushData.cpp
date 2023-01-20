#include "pch.hpp"
#include "StampBrushData.h"

#include "TextureManager.h"

void StampBrushData::SetUniforms()
{
	glBindTexture(GL_TEXTURE_2D, StampTexture);
	glUniform1f(4, StampIntervalRatio);
	glUniform1f(5, NoiseFactor);
	glUniform1f(6, RotationRand);
}

void StampBrushData::DrawProperties()
{
	ImGui::TextUnformatted("Stamp: "); ImGui::SameLine();
	glm::vec2 pos = ImGui::GetCursorPos();
	ImGui::Image(reinterpret_cast<ImTextureID>(TextureManager::Textures[0]), {128, 128 } );
	ImGui::SetCursorPos(pos);
	ImGui::Image(reinterpret_cast<ImTextureID>(StampTexture), {128, 128 });
	ImGui::DragFloat("Interval", &StampIntervalRatio, 0.005f, 0.01f, 3.0f, "%.2f");
	ImGui::DragFloat("Noise factor", &NoiseFactor, 0.01f, 0.0f, 3.0f, "%.2f");
	ImGui::DragFloat("Rotation randomness", &RotationRand, 0.01f, 0.0f, 2.0f, "%.2f");
}
