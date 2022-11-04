#include "pch.hpp"
#include "Application.h"

#include "ArticulatedLineEngine.h"
#include "CanvasPanel.h"
#include "Drawing.h"
#include "Project.h"
#include "Stroke.h"

#include <implot.h>

Application::Application()
{
	Window = std::make_unique<class Window>();
}

void Application::Run()
{
	GenDefaultProject();

	ArticulatedLineEngine alEngine{};
	alEngine.Init(); // warning: not destroy yet.
	Stroke s;
	s.Position = { {0.3f, 0.3f}, {0.5f, 0.9f}, {0.6f, 0.9} };
	s.Width = { 0.3f, 0.3f, 0.3f };
	s.UploadPositionData();
	s.UploadWidthData();
	glBindFramebuffer(GL_FRAMEBUFFER, ActiveProject->MainDrawing->FrameBuffer);
	auto [width, height] = ActiveProject->MainDrawing->GetSizeInPixel();

	glDisable(GL_CULL_FACE);

	glViewport(0, 0, width, height);
	glUseProgram(alEngine.Program);
	glBindVertexArray(s.VertexArray);
	glDrawArrays(GL_LINE_STRIP, 0, 3);
	glBindFramebuffer(GL_FRAMEBUFFER, 0);

	while (!Window->ShouldClose())
	{
		Window->BeginFrame();
		ImPlot::ShowDemoWindow();
		ImGui::ShowDemoWindow();
		ActiveProject->CanvasPanel.Draw();
		Window->EndFrame();
	}
}

void Application::GenDefaultProject()
{
	ActiveProject = std::make_unique<Project>();
	EntityObject::Registry = &ActiveProject->Registry;

	auto drawing = std::make_unique<Drawing>();
	drawing->UpperLeft = {0.0f, 0.0f};
	drawing->LowerRight = {0.297f, 0.21f};
	drawing->Dpi = 144.0f;
	drawing->GenRenderTarget();
	ActiveProject->MainDrawing = std::move(drawing);

	CanvasPanel canvas;
	canvas.ActiveDrawing = ActiveProject->MainDrawing.get();
	ActiveProject->CanvasPanel = std::move(canvas);
}
