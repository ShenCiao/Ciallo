#include "pch.hpp"
#include "Application.h"

#include "CanvasPanel.h"
#include "Drawing.h"
#include "Project.h"

#include <implot.h>
#include <glm/gtc/type_ptr.hpp>
#include "RenderingSystem.h"

Application::Application()
{
	// Window has opengl context. First init;
	Window = std::make_unique<class Window>();
	RenderingSystem::Init();
}

void Application::Run()
{
	GenDefaultProject();
	while (!Window->ShouldClose())
	{
		Window->BeginFrame();
		ImPlot::ShowDemoWindow();
		ImGui::ShowDemoWindow();
		ActiveProject->CanvasPanel->DrawAndRunTool();
		ActiveProject->CanvasPanel->ActiveDrawing->ArrangementSystem.Run();
		RenderingSystem::RenderDrawing(ActiveProject->CanvasPanel->ActiveDrawing);

		// --------------------------
		auto start = chrono::high_resolution_clock::now();
		auto polygons = ActiveProject->CanvasPanel->ActiveDrawing->ArrangementSystem.PointQuery(ActiveProject->CanvasPanel->MousePosOnDrawing);
		chrono::duration<double, std::milli> duration = chrono::high_resolution_clock::now() - start;

		for(auto& [stroke, polys] : ActiveProject->CanvasPanel->ActiveDrawing->ArrangementSystem.QueryResultsContainer)
		{
			polygons.insert(polygons.end(), polys.begin(), polys.end());
		}
		for(auto& polygon : polygons)
		{
			
			glm::mat4 mvp = ActiveProject->CanvasPanel->ActiveDrawing->GetViewProjMatrix();
			glUseProgram(RenderingSystem::Polygon->Program);
			glUniformMatrix4fv(0, 1, GL_FALSE, glm::value_ptr(mvp)); // mvp
			glUniform4f(1, 0, 0, 0, 0.3);
			glEnable(GL_BLEND);

			GLuint vbo = 0, vao = 0;
			glCreateBuffers(1, &vbo);
			glNamedBufferData(vbo, polygon.size() * sizeof(glm::vec2), polygon.data(), GL_DYNAMIC_DRAW);


			glCreateVertexArrays(1, &vao);
			glEnableVertexArrayAttrib(vao, 0);
			glVertexArrayAttribBinding(vao, 0, 0);
			glVertexArrayAttribFormat(vao, 0, 2, GL_FLOAT, GL_FALSE, 0);
			glVertexArrayVertexBuffer(vao, 0, vbo, 0, sizeof(glm::vec2));

			glBindFramebuffer(GL_FRAMEBUFFER, ActiveProject->CanvasPanel->ActiveDrawing->FrameBuffer);
			glBindVertexArray(vao);
			glDrawArrays(GL_TRIANGLE_FAN, 0, polygon.size());
			glBindFramebuffer(GL_FRAMEBUFFER, 0);

			glDeleteBuffers(1, &vbo);
			glDeleteVertexArrays(1, &vao);
		}
		
		// --------------------------
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

	ActiveProject->CanvasPanel = std::make_unique<CanvasPanel>();
	ActiveProject->CanvasPanel->ActiveDrawing = ActiveProject->MainDrawing.get();
}
