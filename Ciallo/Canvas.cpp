#include "pch.hpp"
#include "Canvas.h"
#include "CanvasEvent.h"

#include "Brush.h"
#include "TextureManager.h"
#include "TempLayers.h"
#include "Loader.h"
#include "TimelineManager.h"

#include <glm/gtx/transform.hpp>

Canvas::Canvas()
{
	Dpi = 144.0f;
}

void Canvas::DrawUI()
{
	const ImGuiWindowFlags flags =
		ImGuiWindowFlags_NoScrollWithMouse | ImGuiWindowFlags_NoScrollbar | ImGuiWindowFlags_NoCollapse |
		ImGuiWindowFlags_MenuBar;

	// Begin canvas
	ImGui::PushStyleVar(ImGuiStyleVar_WindowPadding, {.0f, .0f});
	ImGui::Begin("Canvas", nullptr, flags);
	ImGui::PopStyleVar();
	ImGui::BeginMenuBar();

	if (ImGui::BeginMenu("Layers"))
	{
		auto& layers = R.ctx().get<TempLayers>();
		ImGui::Checkbox("Final only", &layers.FinalOnly);
		ImGui::Checkbox("Hide Fill", &layers.HideFill);
		ImGui::EndMenu();
	}

	if (ImGui::Button("Copy Markers")) {
		auto& tm = R.ctx().get<TimelineManager>();
		tm.CopyFillMarker(tm.GetRenderDrawing());
	}

	if (ImGui::Button("Paste Markers")) {
		auto& tm = R.ctx().get<TimelineManager>();
		tm.PasteFillMarker(tm.GetCurrentDrawing());
	}

	if (ImGui::Button("Export")) Export();

	if (ImGui::Button("ExportFrames")) {
		R.ctx().get<TimelineManager>().ExportAllFrames();
	}

	ImGui::ColorEdit4("Background Color", glm::value_ptr(R.ctx().get<TempLayers>().BackgroundColor), ImGuiColorEditFlags_NoInputs|ImGuiColorEditFlags_NoLabel);
	ImGui::EndMenuBar();

	// Canvas content
	ImGui::PushStyleVar(ImGuiStyleVar_WindowPadding, {.0f, .0f});
	auto panel = ImGui::GetCurrentWindow();
	glm::vec2 contentSize = glm::vec2(panel->InnerRect.GetSize());
	Image = RenderableTexture(static_cast<int>(contentSize.x), static_cast<int>(contentSize.y));
	ImGui::Image(reinterpret_cast<ImTextureID>(Image.ColorTexture), panel->InnerRect.GetSize());

	// Set viewport
	glm::vec2 worldSize = contentSize / Dpi * 0.0254f; // in meter
	Viewport.Max = Viewport.Min + worldSize;
	Viewport.UploadMVP();
	
	ImGui::End();
	ImGui::PopStyleVar();
}

glm::ivec2 Canvas::GetSizePixel() const
{
	return Viewport.GetSizePixel(Dpi);
}

void Canvas::Export() const
{
	TextureManager::SaveTexture(Image.ColorTexture, "canvas");
}

void Canvas::Run()
{
	// Dealing with interaction events.
	ImGui::Begin("Canvas");
	auto panel = ImGui::GetCurrentWindow();
	// Invisible button for interaction
	ImGui::SetCursorScreenPos(panel->InnerRect.Min);
	ImGui::InvisibleButton("CanvasInteraction", panel->InnerRect.GetSize(),
	                       ImGuiButtonFlags_MouseButtonLeft | ImGuiButtonFlags_MouseButtonRight |
	                       ImGuiButtonFlags_MouseButtonMiddle);
	EventDispatcher.trigger(BeforeInteraction{});
	// mouse position relative to image
	glm::vec2 mousePosPixel = ImGui::GetMousePos() - panel->InnerRect.Min;
	glm::vec2 mousePos = mousePosPixel / panel->InnerRect.GetSize() * Viewport.GetSize() + Viewport.Min;
	float pressure = ImGui::GetIO().PenPressure;
	// Tool interaction
	auto interaction = [&]()
	{
		// Active item is the invisible button, "CanvasInteraction".
		if (ImGui::IsMouseClicked(0) && ImGui::IsItemActivated())
		{
			if (IsDragging)
			{
				IsDragging = false;
				auto duration = chrono::high_resolution_clock::now() - StartDraggingTimePoint;
				EventDispatcher.trigger(DragEnd{
					mousePos, mousePosPixel, pressure, PrevMousePos - mousePos, ImGui::GetMouseDragDelta(), duration
				});
			}
			StartDraggingTimePoint = chrono::high_resolution_clock::now();
			EventDispatcher.trigger(ClickOrDragStart{mousePos, mousePosPixel});
			PrevMousePos = mousePos;
			PrevMousePosPixel = ImGui::GetMousePos();
			return;
		}
	
		if (ImGui::IsMouseDragging(0) && !ImGui::IsItemActivated() && ImGui::IsItemActive())
		{
			IsDragging = true;
			auto duration = chrono::high_resolution_clock::now() - StartDraggingTimePoint;
			EventDispatcher.trigger(Dragging{
				mousePos, mousePosPixel, pressure, mousePos - PrevMousePos, ImGui::GetMouseDragDelta(), duration
			});
			PrevMousePos = mousePos;
			PrevMousePosPixel = ImGui::GetMousePos();
			return;
		}
	
		if (IsDragging && !ImGui::IsMouseDragging(0))
		{
			IsDragging = false;
			auto duration = chrono::high_resolution_clock::now() - StartDraggingTimePoint;
			EventDispatcher.trigger(DragEnd{
				mousePos, mousePosPixel, pressure, mousePos - PrevMousePos, ImGui::GetMouseDragDelta(), duration
			});
			PrevMousePos = mousePos;
			PrevMousePosPixel = ImGui::GetMousePos();
			return;
		}
	
		if (ImGui::IsItemHovered() && !IsDragging && !ImGui::IsMouseClicked(0) && !ImGui::IsKeyDown(ImGuiKey_Space))
		{
			EventDispatcher.trigger(Hovering{mousePos, mousePosPixel});
			PrevMousePosPixel = ImGui::GetMousePos();
		}
	
		if (ImGui::IsItemHovered() && !IsDragging && !ImGui::IsMouseClicked(0) && ImGui::IsKeyDown(ImGuiKey_Space))
		{
			glm::vec2 delta = mousePos - PrevMousePos;
			
			Viewport.Min -= delta;
			Viewport.Max -= delta;
			Viewport.UploadMVP();
			PrevMousePosPixel = ImGui::GetMousePos();
		}
		PrevMousePos = mousePos;
	};
	interaction();
	EventDispatcher.trigger(AfterInteraction{});

	// Deal with zoom in and out
	// Theoretically, zoom in and out canvas is another type of tool, but it's more convenient to put it here.
	float mouseWheelScroll = ImGui::GetIO().MouseWheel;
	
	if (ImGui::IsItemHovered() && mouseWheelScroll != 0.0f) {
		float zoomFactor = glm::pow(1.1f, mouseWheelScroll);
		Dpi *= zoomFactor;
		Viewport.Min = (Viewport.Min - mousePos) / zoomFactor + mousePos;
		Viewport.Max = (Viewport.Max - mousePos) / zoomFactor + mousePos;
	}
	
	if (ImGui::IsItemHovered() && ImGui::IsMouseDragging(ImGuiMouseButton_Middle)) {
		glm::vec2 delta = ImGui::GetIO().MouseDelta;
		glm::vec2 contentSize = panel->InnerRect.GetSize();
		Viewport.Min -= delta / contentSize * Viewport.GetSize();
		Viewport.Max -= delta / contentSize * Viewport.GetSize();
	}
	ImGui::End();
}
