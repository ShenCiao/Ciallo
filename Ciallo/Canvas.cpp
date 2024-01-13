#include "pch.hpp"
#include "Canvas.h"
#include "CanvasEvent.h"

#include "StrokeContainer.h"
#include "Stroke.h"
#include "Brush.h"
#include "TextureManager.h"
#include "TempLayers.h"
#include "Loader.h"
#include "TimelineManager.h"

#include <glm/gtx/transform.hpp>

Canvas::Canvas()
{
	Viewport.Min = { 0.0f, 0.0f };
	Viewport.Max = { 0.297f, 0.21f };
	Dpi = 144.0f;
	GenRenderTarget();
}

void Canvas::DrawUI()
{
	const ImGuiWindowFlags flags =
		ImGuiWindowFlags_NoScrollWithMouse | ImGuiWindowFlags_HorizontalScrollbar |
		ImGuiWindowFlags_AlwaysHorizontalScrollbar | ImGuiWindowFlags_AlwaysVerticalScrollbar |
		ImGuiWindowFlags_MenuBar;

	ImGui::PushStyleVar(ImGuiStyleVar_WindowPadding, {.0f, .0f});
	ImGui::Begin("Canvas", nullptr, flags);

	ImGui::PopStyleVar();
	ImGui::BeginMenuBar();

	if (ImGui::BeginMenu("Load Model"))
	{
		if (ImGui::MenuItem("Monkey"))
			Loader::LoadCsv("./models/monkey.csv");
		if (ImGui::MenuItem("Bear"))
			Loader::LoadCsv("./models/bear.csv");
		if (ImGui::MenuItem("Dango"))
			Loader::LoadCsv("./models/dango.csv", 0.003f);
		if (ImGui::MenuItem("Totoro"))
			Loader::LoadCsv("./models/totoro.csv", 0.001f);
		if (ImGui::MenuItem("Grid"))
			Loader::LoadCsv("./models/grid.csv");
		if (ImGui::MenuItem("Cat"))
			Loader::LoadAnimation("./models/cat");
		if (ImGui::MenuItem("Butterfly"))
			Loader::LoadAnimation("./models/butterfly", 0.005f);
		if (ImGui::MenuItem("1"))
			Loader::LoadCsv("./models/1.csv", 0.004f);
		if (ImGui::MenuItem("2"))
			Loader::LoadCsv("./models/2.csv", 0.004f);
		if (ImGui::MenuItem("3"))
			Loader::LoadCsv("./models/3.csv", 0.004f);
		ImGui::EndMenu();
	}

	if (ImGui::Button("Save Project"))
	{
		Loader::SaveProject("./project/project");
	}

	if (ImGui::Button("Load Project"))
	{
		Loader::ShouldLoadProject = true;
	}

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

	static int n = 1;
	ImGui::PushItemWidth(200);
	ImGui::PopItemWidth();
	ImGui::EndMenuBar();

	ImGui::PushStyleVar(ImGuiStyleVar_WindowPadding, {.0f, .0f});
	auto panel = ImGui::GetCurrentWindow();
	glm::vec2 drawingScreenOrigin = ImGui::GetCursorScreenPos();

	ImGui::Image(ImTextureID(Image.ColorTexture), Image.Size());
	// mouse position relative to image
	glm::vec2 mousePosPixel = ImGui::GetMousePos() - drawingScreenOrigin;
	glm::vec2 mousePos = mousePosPixel / (Viewport.GetSizePixelFloat(Dpi) * Zoom) * Viewport.GetSize();
	float pressure = ImGui::GetIO().PenPressure;

	// Invisible button for interaction
	ImGui::SetCursorScreenPos(panel->InnerRect.Min);
	ImGui::InvisibleButton("CanvasInteractor", panel->InnerRect.GetSize(),
	                       ImGuiButtonFlags_MouseButtonLeft | ImGuiButtonFlags_MouseButtonRight |
	                       ImGuiButtonFlags_MouseButtonMiddle);
	EventDispatcher.trigger(BeforeInteraction{});


	auto interact = [&]()
	{
		// Active item is the invisible button, "CanvasInteractor".
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
			glm::vec2 delta = ImGui::GetMousePos() - PrevMousePosPixel;
			ImGui::SetScrollX(ImGui::GetScrollX() - delta.x);
			ImGui::SetScrollY(ImGui::GetScrollY() - delta.y);
			PrevMousePosPixel = ImGui::GetMousePos();
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

void Canvas::RenderContentNTimes(int n)
{
	// RenderableTexture& target = Image;
	// glEnable(GL_BLEND);
	// glBlendFuncSeparate(GL_SRC_ALPHA, GL_ONE_MINUS_SRC_ALPHA, GL_ONE, GL_ONE_MINUS_SRC_ALPHA);
	// target.BindFramebuffer();
	// glClearColor(1, 1, 1, 1);
	// glClear(GL_COLOR_BUFFER_BIT);
	//
	// std::vector<Stroke*> strokes;
	// auto& strokeEs = R.ctx().get<StrokeContainer>().StrokeEs;
	// for (entt::entity strokeE : strokeEs)
	// {
	// 	strokes.push_back(&R.get<Stroke>(strokeE));
	// }
	// auto& brush = R.get<Brush>(strokes[0]->BrushE);
	//
	// std::default_random_engine rng;
	// std::uniform_real dist(-1.0f, 1.0f);
	// auto& canvas = R.ctx().get<Canvas>();
	//
	// brush.Use();
	// auto start = chrono::high_resolution_clock::now();
	// for (int i = 0; i < n; ++i)
	// {
	// 	glm::vec2 randOffset = glm::vec2(dist(rng), dist(rng)) * canvas.Viewport.GetSize() / 2.0f;
	// 	Viewport.UploadMVP(glm::translate(glm::vec3{randOffset, 0.f}));
	// 	Viewport.BindMVPBuffer();
	// 	for (auto* stroke : strokes)
	// 	{
	// 		brush.SetUniforms();
	// 		stroke->SetUniforms();
	// 		stroke->LineDrawCall();
	// 	}
	// }
	// chrono::duration<double, std::milli> duration = chrono::high_resolution_clock::now() - start;
	// spdlog::info("{}ms", duration.count());
	// glBindFramebuffer(GL_FRAMEBUFFER, 0);
	auto& strokeEs = R.ctx().get<StrokeContainer>().StrokeEs;
	auto dup = strokeEs;
	for (int i = 0; i < n; ++i)
	{
		std::copy(dup.begin(), dup.end(), std::back_inserter(strokeEs));
	}
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
	if (ImGui::IsKeyPressed(ImGuiKey_W)) {
		if(Dpi < 1000.0f)
			Dpi *= 1.1f;
		GenRenderTarget();
	}
	if (ImGui::IsKeyPressed(ImGuiKey_S)) {
		Dpi /= 1.1f;
		GenRenderTarget();
	}
}
