#include "pch.hpp"
#include "Canvas.h"
#include "CanvasEvent.h"

#include "StrokeContainer.h"
#include "Stroke.h"
#include "Brush.h"
#include "RenderingSystem.h"

void Canvas::DrawUI()
{
	const ImGuiWindowFlags flags =
		ImGuiWindowFlags_NoScrollWithMouse | ImGuiWindowFlags_HorizontalScrollbar |
		ImGuiWindowFlags_AlwaysHorizontalScrollbar | ImGuiWindowFlags_AlwaysVerticalScrollbar;
	ImGui::PushStyleVar(ImGuiStyleVar_WindowPadding, {.0f, .0f});
	ImGui::Begin("Canvas", nullptr, flags);

	auto panel = ImGui::GetCurrentWindow();
	glm::vec2 drawingScreenOrigin = ImGui::GetCursorScreenPos();

	ImGui::Image(ImTextureID(Image.ColorTexture), Image.Size());
	// mouse position relative to image
	glm::vec2 mousePosPixel = ImGui::GetMousePos() - drawingScreenOrigin;
	glm::vec2 mousePos = mousePosPixel / (Viewport.GetSizePixelFloat(Dpi) * Zoom) * Viewport.GetSize();

	// Invisible button for interaction
	ImGui::SetCursorScreenPos(panel->InnerRect.Min);
	ImGui::InvisibleButton("CanvasInteraction", panel->InnerRect.GetSize(),
	                       ImGuiButtonFlags_MouseButtonLeft | ImGuiButtonFlags_MouseButtonRight |
	                       ImGuiButtonFlags_MouseButtonMiddle);
	EventDispatcher.trigger(BeforeInteraction{});

	auto interact = [&]()
	{
		if (ImGui::IsMouseClicked(0) && ImGui::IsItemActivated())
		{
			if (IsDragging)
			{
				IsDragging = false;
				auto duration = chrono::high_resolution_clock::now() - StartDraggingTimePoint;
				EventDispatcher.trigger(DragEnd{
					mousePos, mousePosPixel, PrevMousePos - mousePos, ImGui::GetMouseDragDelta(), duration
				});
			}
			StartDraggingTimePoint = chrono::high_resolution_clock::now();
			EventDispatcher.trigger(ClickOrDragStart{mousePos, mousePosPixel});
			PrevMousePos = mousePos;
			return;
		}

		if (ImGui::IsMouseDragging(0) && !ImGui::IsItemActivated() && ImGui::IsItemActive())
		{
			IsDragging = true;
			auto duration = chrono::high_resolution_clock::now() - StartDraggingTimePoint;
			EventDispatcher.trigger(Dragging{
				mousePos, mousePosPixel, PrevMousePos - mousePos, ImGui::GetMouseDragDelta(), duration
			});
			return;
		}

		if (IsDragging && !ImGui::IsMouseDragging(0))
		{
			IsDragging = false;
			auto duration = chrono::high_resolution_clock::now() - StartDraggingTimePoint;
			EventDispatcher.trigger(DragEnd{
				mousePos, mousePosPixel, PrevMousePos - mousePos, ImGui::GetMouseDragDelta(), duration
			});
			return;
		}

		if (ImGui::IsItemHovered() && !IsDragging && !ImGui::IsMouseClicked(0))
		{
			EventDispatcher.trigger(Hovering{mousePos, mousePosPixel});
		}
	};

	interact();
	EventDispatcher.trigger(AfterInteraction{});
	ImGui::End();
	ImGui::PopStyleVar();
}

void Canvas::GenRenderTarget()
{
	auto size = Viewport.GetSizePixel(Dpi);
	Image = RenderableTexture(size.x, size.y);
}

void Canvas::Render()
{
	auto& strokeEs = R.ctx().get<StrokeContainer>().StrokeEs;

	RenderableTexture& target = Image;
	glEnable(GL_BLEND);
	glBlendFuncSeparate(GL_SRC_ALPHA, GL_ONE_MINUS_SRC_ALPHA, GL_ONE, GL_ONE_MINUS_SRC_ALPHA);
	target.BindFramebuffer();
	glClearColor(1, 1, 1, 1);
	glClear(GL_COLOR_BUFFER_BIT);
	Viewport.UploadMVP();
	Viewport.BindMVPBuffer();
	for (entt::entity e : strokeEs)
	{
		auto& stroke = R.get<Stroke>(e);
		auto& brush = R.get<Brush>(stroke.Brush);
		brush.Use();
		brush.SetUniforms();
		stroke.SetUniforms();
		stroke.LineDrawCall();
	}
	glBindFramebuffer(GL_FRAMEBUFFER, 0);
}

glm::ivec2 Canvas::GetSizePixel() const
{
	return Viewport.GetSizePixel(Dpi);
}
