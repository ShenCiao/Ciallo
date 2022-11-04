#include "pch.hpp"
#include "Application.h"

#include "ArticulatedLineEngine.h"
#include "CanvasPanel.h"
#include "Drawing.h"
#include "Project.h"
#include "Stroke.h"

#include <implot.h>
#include <glm/gtc/type_ptr.hpp>
#include <glm/gtc/constants.hpp>

Application::Application()
{
	Window = std::make_unique<class Window>();
}

void Application::Run()
{
	GenDefaultProject();

	Stroke s;
	glm::vec2 size = ActiveProject->MainDrawing->GetWorldSize();
	float r = 1.f/(1.f+glm::golden_ratio<float>());
	s.Position = {
		{size.x / 2.f, r * size.y},
		{r*size.x, (1-r)*size.y},
		{(1-r)*size.x, (1 - r) * size.y}
	};
	s.Width = { 0.001f, 0.001f, 0.001f };
	s.UploadPositionData();
	s.UploadWidthData(); 

	ArticulatedLineEngine alEngine{};
	
	glBindFramebuffer(GL_FRAMEBUFFER, ActiveProject->MainDrawing->FrameBuffer);
	glDisable(GL_CULL_FACE);
	glEnable(GL_BLEND);
	glBlendFunc(GL_SRC_ALPHA, GL_ONE_MINUS_SRC_ALPHA);

	auto pixelSize = ActiveProject->MainDrawing->GetSizeInPixel();
	glViewport(0, 0, pixelSize.x, pixelSize.y);
	glUseProgram(alEngine.Program);
	glm::mat4 mvp = ActiveProject->MainDrawing->GetViewProjMatrix();
	glUniformMatrix4fv(0, 1, GL_FALSE, glm::value_ptr(mvp));
	glBindVertexArray(s.VertexArray);
	glDrawArrays(GL_LINE_LOOP, 0, 3);

	glBindFramebuffer(GL_FRAMEBUFFER, 0);

	while (!Window->ShouldClose())
	{
		glm::vec2 mousePos;
		Window->BeginFrame();
		ImPlot::ShowDemoWindow();
		ImGui::ShowDemoWindow();
		ActiveProject->CanvasPanel.Draw(&mousePos);

		s.Position = std::vector<Geom::Point>{ { mousePos.x, mousePos.y }, {mousePos.x + 0.05f, mousePos.y + 0.05f} };
		s.Width = std::vector(2, 0.001f);
		s.UploadPositionData();
		s.UploadWidthData();

		glBindFramebuffer(GL_FRAMEBUFFER, ActiveProject->MainDrawing->FrameBuffer);
		glClearColor(1, 1, 1, 1);
		glClear(GL_COLOR_BUFFER_BIT);
		glDisable(GL_CULL_FACE);
		glEnable(GL_BLEND);
		glBlendFunc(GL_SRC_ALPHA, GL_ONE_MINUS_SRC_ALPHA);

		auto pixelSize = ActiveProject->MainDrawing->GetSizeInPixel();
		glViewport(0, 0, pixelSize.x, pixelSize.y);
		glUseProgram(alEngine.Program);
		glm::mat4 mvp = ActiveProject->MainDrawing->GetViewProjMatrix();
		glUniformMatrix4fv(0, 1, GL_FALSE, glm::value_ptr(mvp));
		glBindVertexArray(s.VertexArray);
		glDrawArrays(GL_LINE_LOOP, 0, 2);

		glBindFramebuffer(GL_FRAMEBUFFER, 0);

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
