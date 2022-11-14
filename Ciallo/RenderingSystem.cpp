#include "pch.hpp"
#include "RenderingSystem.h"

#include <glm/gtc/type_ptr.hpp>

void RenderingSystem::Init()
{
	ArticulatedLine = std::make_unique<ArticulatedLineEngine>();
	Polygon = std::make_unique<PolygonEngine>();
}

void RenderingSystem::RenderDrawing(Drawing* drawing)
{
	glBindFramebuffer(GL_FRAMEBUFFER, drawing->FrameBuffer);
	glClearColor(1, 1, 1, 1);
	glClear(GL_COLOR_BUFFER_BIT);
	glDisable(GL_CULL_FACE);
	glEnable(GL_BLEND);
	glBlendFunc(GL_SRC_ALPHA, GL_ONE_MINUS_SRC_ALPHA);

	auto pixelSize = drawing->GetSizeInPixel();
	glViewport(0, 0, pixelSize.x, pixelSize.y);
	glUseProgram(ArticulatedLine->Program);
	glm::mat4 mvp = drawing->GetViewProjMatrix();
	glUniformMatrix4fv(0, 1, GL_FALSE, glm::value_ptr(mvp)); // mvp
	glUniform4f(1, 0.5, 0.5, 0.5, 1);// color

	for(auto& s : drawing->Strokes)
	{
		ArticulatedLine->DrawStroke(s.get());
	}

	glUseProgram(0);
	glBindFramebuffer(GL_FRAMEBUFFER, 0);
}
