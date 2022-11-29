#include "pch.hpp"
#include "Application.h"

#include "CanvasPanel.h"
#include "Drawing.h"
#include "Project.h"
#include "RenderingSystem.h"
#include "CubicBezier.h"

#include <implot.h>
#include <glm/gtc/type_ptr.hpp>

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
		auto tmpPolygon = ActiveProject->CanvasPanel->ActiveDrawing->ArrangementSystem.PointQuery(ActiveProject->CanvasPanel->MousePosOnDrawing);
		std::vector<std::vector<Geom::Polyline>> polygons;
		if(tmpPolygon.size() != 0)
			polygons.push_back(tmpPolygon);

		for(auto& [stroke, polys] : ActiveProject->CanvasPanel->ActiveDrawing->ArrangementSystem.QueryResultsContainer)
		{
			polygons.insert(polygons.end(), polys.begin(), polys.end());
		}

		glBindFramebuffer(GL_FRAMEBUFFER, ActiveProject->CanvasPanel->ActiveDrawing->FrameBuffer);
		glEnable(GL_BLEND);
		glEnable(GL_STENCIL_TEST);
		glDisable(GL_DEPTH_TEST);
		glm::mat4 mvp = ActiveProject->CanvasPanel->ActiveDrawing->GetViewProjMatrix();
		glUseProgram(RenderingSystem::Polygon->Program);
		glUniformMatrix4fv(0, 1, GL_FALSE, glm::value_ptr(mvp)); // mvp
		glUniform4f(1, 0.f, 0.f, 0.f, 0.3f); // color

		for(auto& polygonWithHoles : polygons)
		{
			PolygonEngine::DrawPolygon(polygonWithHoles);
		}
		glBindFramebuffer(GL_FRAMEBUFFER, 0);
		
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
