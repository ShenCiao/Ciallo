#include "pch.hpp"
#include "CanvasPanel.h"

#include "arr_print.h"

CanvasPanel::CanvasPanel()
{
	PaintTool = std::make_unique<class PaintTool>(this);
	ActiveTool = PaintTool.get();
	EditTool = std::make_unique<class EditTool>(this);
	FillTool = std::make_unique<class FillTool>(this);
}

CanvasPanel::~CanvasPanel()
{
	DeleteOverlayBuffers();
}

void CanvasPanel::GenOverlayBuffers()
{
	DeleteOverlayBuffers();
	int width = ActiveDrawing->GetSizeInPixel().x;
	int height = ActiveDrawing->GetSizeInPixel().y;
	// color buffer
	
	glCreateTextures(GL_TEXTURE_2D, 1, &OverlayColorTexture);
	glTextureParameteri(OverlayColorTexture, GL_TEXTURE_MIN_FILTER, GL_NEAREST);
	glTextureParameteri(OverlayColorTexture, GL_TEXTURE_MAG_FILTER, GL_NEAREST);
	glTextureParameteri(OverlayColorTexture, GL_TEXTURE_WRAP_S, GL_CLAMP_TO_EDGE);
	glTextureParameteri(OverlayColorTexture, GL_TEXTURE_WRAP_T, GL_CLAMP_TO_EDGE);

	glTextureStorage2D(OverlayColorTexture, 1, GL_RGBA8, width, height);

	// stencil and depth
	glCreateTextures(GL_TEXTURE_2D, 1, &OverlayDepthStencilTexture);
	glTextureParameteri(OverlayDepthStencilTexture, GL_TEXTURE_MIN_FILTER, GL_NEAREST);
	glTextureParameteri(OverlayDepthStencilTexture, GL_TEXTURE_MAG_FILTER, GL_NEAREST);
	glTextureParameteri(OverlayDepthStencilTexture, GL_TEXTURE_WRAP_S, GL_CLAMP_TO_EDGE);
	glTextureParameteri(OverlayDepthStencilTexture, GL_TEXTURE_WRAP_T, GL_CLAMP_TO_EDGE);

	glTextureStorage2D(OverlayDepthStencilTexture, 1, GL_DEPTH24_STENCIL8, width, height);

	glCreateFramebuffers(1, &OverlayFrameBuffer);
	glNamedFramebufferTexture(OverlayFrameBuffer, GL_COLOR_ATTACHMENT0, OverlayColorTexture, 0);
	glNamedFramebufferTexture(OverlayFrameBuffer, GL_DEPTH_STENCIL_ATTACHMENT, OverlayDepthStencilTexture, 0);

	if (glCheckNamedFramebufferStatus(OverlayFrameBuffer, GL_FRAMEBUFFER) != GL_FRAMEBUFFER_COMPLETE)
	{
		throw std::runtime_error("Framebuffer incomplete");
	}
}

void CanvasPanel::DeleteOverlayBuffers()
{
	glDeleteTextures(1, &OverlayColorTexture);
	glDeleteTextures(1, &OverlayDepthStencilTexture);
	glDeleteFramebuffers(1, &OverlayFrameBuffer);
	OverlayColorTexture = 0;
	OverlayDepthStencilTexture = 0;
	OverlayFrameBuffer = 0;
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

	ImGui::Image(reinterpret_cast<ImTextureID>(ActiveDrawing->ColorTexture), ImVec2(
		             static_cast<float>(ActiveDrawing->GetSizeInPixel().x),
		             static_cast<float>(ActiveDrawing->GetSizeInPixel().y)));
	ImGui::SetCursorScreenPos(drawingScreenOrigin);
	ImGui::Image(reinterpret_cast<ImTextureID>(OverlayColorTexture), ImVec2(
		             static_cast<float>(ActiveDrawing->GetSizeInPixel().x),
		             static_cast<float>(ActiveDrawing->GetSizeInPixel().y)));

	glm::vec2 mousePosOnDrawingInPixel = ImGui::GetMousePos() - drawingScreenOrigin;
	MousePosOnDrawingInPixel = {mousePosOnDrawingInPixel.x, mousePosOnDrawingInPixel.y};
	// Do remember to set the cursor to correct place.
	MousePosOnDrawing = mousePosOnDrawingInPixel / (ActiveDrawing->GetSizeInPixelFloat() * Zoom) *
		ActiveDrawing->GetWorldSize();

	ImGui::SetCursorScreenPos(panel->InnerRect.Min);
	ImGui::InvisibleButton(std::format("CanvasInteraction").c_str(), panel->InnerRect.GetSize(),
	                       ImGuiButtonFlags_MouseButtonLeft | ImGuiButtonFlags_MouseButtonRight |
	                       ImGuiButtonFlags_MouseButtonMiddle);


	ActiveTool->Run();

	ImGui::End();
	ImGui::PopStyleVar();

	ImGui::Begin("Toolbox");
	if (ImGui::Button("Paint"))
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

	if (ImGui::Button("Vector Bucket Fill"))
	{
		ActiveTool->Deactivate();
		ActiveTool = FillTool.get();
		ActiveTool->Activate();
	}

	if (ImGui::Button("Print Arrangement"))
	{
		print_arrangement(ActiveDrawing->ArrangementSystem.Arrangement);
	}

	if (ImGui::Button("Print Arrangement Size"))
	{
		print_arrangement_size(ActiveDrawing->ArrangementSystem.Arrangement);
	}

	ActiveTool->DrawProperties();

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
