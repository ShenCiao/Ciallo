#include "pch.hpp"
#include "CanvasPanel.h"

#define STB_IMAGE_IMPLEMENTATION
#include <stb_image.h>

CanvasPanel::CanvasPanel()
{
	PaintTool = std::make_unique<class PaintTool>(this);
	ActiveTool = PaintTool.get();
	EditTool = std::make_unique<class EditTool>(this);
}

void CanvasPanel::DrawAndRunTool()
{
	const ImGuiWindowFlags flags = ImGuiWindowFlags_NoScrollWithMouse | ImGuiWindowFlags_HorizontalScrollbar |
		ImGuiWindowFlags_AlwaysHorizontalScrollbar | ImGuiWindowFlags_AlwaysVerticalScrollbar;
	ImGui::PushStyleVar(ImGuiStyleVar_WindowPadding, {.0f, .0f});
	ImGui::Begin("Canvas Panel", nullptr, flags);

	auto& io = ImGui::GetIO();
	auto panel = ImGui::GetCurrentWindow();
	glm::vec2 drawingScreenOrigin = ImGui::GetCursorScreenPos();

	ImGui::Image(reinterpret_cast<ImTextureID>(ActiveDrawing->Texture), ImVec2(
		static_cast<float>(ActiveDrawing->GetSizeInPixel().x),
		static_cast<float>(ActiveDrawing->GetSizeInPixel().y)));

	glm::vec2 mousePosOnDrawingInPixel = ImGui::GetMousePos() - drawingScreenOrigin;
	MousePosOnDrawingInPixel = { mousePosOnDrawingInPixel.x, mousePosOnDrawingInPixel.y };
	// Do remember to set the cursor to correct place.
	MousePosOnDrawing = mousePosOnDrawingInPixel / (ActiveDrawing->GetSizeInPixelFloat()*Zoom) *
		ActiveDrawing->GetWorldSize();

	ImGui::SetCursorScreenPos(panel->InnerRect.Min);
	ImGui::InvisibleButton(std::format("CanvasInteraction").c_str(), panel->InnerRect.GetSize(),
                       ImGuiButtonFlags_MouseButtonLeft | ImGuiButtonFlags_MouseButtonRight |
                       ImGuiButtonFlags_MouseButtonMiddle);

	
	ActiveTool->Run();

	ImGui::End();
	ImGui::PopStyleVar();

	ImGui::Begin("Toolbox");
	if(ImGui::Button("Paint"))
	{
		ActiveTool->Deactivate();
		ActiveTool = PaintTool.get();
		ActiveTool->Activate();
	}

	if (ImGui::Button("Edit"))
	{
		ActiveTool->Deactivate();
		ActiveTool = EditTool.get();
		ActiveTool->Activate();
	}
	ImGui::End();

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
