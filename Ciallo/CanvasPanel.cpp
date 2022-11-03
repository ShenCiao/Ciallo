#include "pch.hpp"
#include "CanvasPanel.h"

#define STB_IMAGE_IMPLEMENTATION
#include <stb_image.h>

void CanvasPanel::Draw()
{
	const ImGuiWindowFlags flags = ImGuiWindowFlags_NoScrollWithMouse | ImGuiWindowFlags_HorizontalScrollbar |
		ImGuiWindowFlags_AlwaysHorizontalScrollbar | ImGuiWindowFlags_AlwaysVerticalScrollbar;
	ImGui::PushStyleVar(ImGuiStyleVar_WindowPadding, { .0f, .0f });
	ImGui::Begin("Canvas Panel", nullptr, flags);
	auto& io = ImGui::GetIO();
	ImGui::Image(reinterpret_cast<ImTextureID>(ActiveDrawing->Texture), ImVec2(ActiveDrawing->GetSizeInPixel().first, ActiveDrawing->GetSizeInPixel().second));
	ImGui::End();
	ImGui::PopStyleVar();
	// bool notCloseWindow = true;
	// if(ActiveDrawing == entt::null)
	// {
	// 	
	// }
	// // Padding a whole image size around middle, so it's 3 times.
	// // ImGui::SetNextWindowContentSize(imageSize * 3.0f);
	// ImGui::PushStyleVar(ImGuiStyleVar_WindowPadding, {.0f, .0f});
	// ImGui::Begin("Canvas Panel", &notCloseWindow, flags);
	// if (!notCloseWindow)
	// {
	// 	ImGui::End();
	// 	ImGui::PopStyleVar(1);
	// 	// r.destroy(canvasPanelEntity);
	// 	return;
	// }
	//
	// if (!r.valid(drawingEntity) || !r.all_of<GPUImageCpo>(drawingEntity))
	// {
	// 	ImGui::Text("No active drawing");
	// 	ImGui::End();
	// 	ImGui::PopStyleVar(1);
	// 	return;
	// }
	//
	// auto panel = ImGui::GetCurrentWindow();
	// glm::vec2 innerRectSize = panel->InnerRect.GetSize();
	// glm::vec2 innerRectMin = panel->InnerRect.Min;
	// glm::vec2 innerRectMinWindow = panel->InnerRect.Min - ImGui::GetWindowPos();
	//
	// ImGuiIO io = ImGui::GetIO();
	//
	// // -----------------------------------------------------------------------------
	// // Mouse interaction
	// ImGui::SetCursorScreenPos(innerRectMin);
	//
	// ImGui::InvisibleButton(std::format("CanvasInteraction##{}", canvasPanelEntity).c_str(), innerRectSize,
	//                        ImGuiButtonFlags_MouseButtonLeft | ImGuiButtonFlags_MouseButtonRight |
	//                        ImGuiButtonFlags_MouseButtonMiddle);
	// if (ImGui::IsItemHovered() || ImGui::IsItemActive())
	// {
	// 	if (glm::abs(io.MouseWheel) >= 1.0f)
	// 	{
	// 		const float wheelZoomFactor = 0.1f;
	// 		float zoom_prev = canvasPanelCpo.zoom;
	// 		canvasPanelCpo.zoom += wheelZoomFactor * io.MouseWheel;
	// 		glm::vec2 mouseInnerRect = io.MousePos - innerRectMin;
	// 		canvasPanelCpo.scroll = (canvasPanelCpo.scroll + mouseInnerRect) / zoom_prev * canvasPanelCpo.zoom -
	// 			mouseInnerRect;
	// 	}
	// }
	//
	// if (ImGui::IsItemHovered())
	// {
	// }
	//
	// if (io.MouseDown[2] && ImGui::IsItemActive())
	// {
	// 	ImGui::SetMouseCursor(ImGuiMouseCursor_Hand);
	// 	canvasPanelCpo.scroll = canvasPanelCpo.scroll - glm::vec2(io.MouseDelta);
	// }
	//
	// canvasPanelCpo.zoom = glm::clamp(canvasPanelCpo.zoom, 0.0f, 1000.0f);
	// canvasPanelCpo.scroll = glm::clamp(canvasPanelCpo.scroll, {0.0f, 0.0f},
	//                                    {ImGui::GetScrollMaxX(), ImGui::GetScrollMaxY()});
	// ImGui::SetScrollX(canvasPanelCpo.scroll.x);
	// ImGui::SetScrollY(canvasPanelCpo.scroll.y);
	// // -----------------------------------------------------------------------------
	// glm::vec2 imageStartPosition = {
	// 	imageSize.x * 3.0f >= innerRectSize.x ? imageSize.x : innerRectSize.x / 2.0f - imageSize.x / 2.0f,
	// 	imageSize.y * 3.0f >= innerRectSize.y
	// 		? imageSize.y + innerRectMinWindow.y
	// 		: innerRectSize.y / 2.0f - imageSize.y / 2.0f + innerRectMinWindow.y
	// };
	// ImGui::SetCursorPosX(imageStartPosition.x);
	// ImGui::SetCursorPosY(imageStartPosition.y);
	// ImGui::Image(vulkanImageCpo.id, imageSize);
	//
	// ImGui::End();
	// ImGui::PopStyleVar();
}

void CanvasPanel::LoadTestImage()
{
	unsigned char* image_data = stbi_load("./images/myaamori.jpg", &image_width, &image_height, NULL, 4);
	if (image_data == NULL)
		throw std::runtime_error("Can not load image");
	
	glCreateTextures(GL_TEXTURE_2D, 1, &image_texture);
	
	// Setup filtering parameters for display
	glTextureParameteri(image_texture, GL_TEXTURE_MIN_FILTER, GL_LINEAR);
	glTextureParameteri(image_texture, GL_TEXTURE_MAG_FILTER, GL_LINEAR);
	glTextureParameteri(image_texture, GL_TEXTURE_WRAP_S, GL_CLAMP_TO_EDGE); // This is required on WebGL for non power-of-two textures
	glTextureParameteri(image_texture, GL_TEXTURE_WRAP_T, GL_CLAMP_TO_EDGE); // Same
	
	glTextureStorage2D(image_texture, 1, GL_RGBA8, image_width, image_height);
	glTextureSubImage2D(image_texture, 0, 0, 0, image_width, image_height, GL_RGBA, GL_UNSIGNED_BYTE, image_data);

	stbi_image_free(image_data);
}
