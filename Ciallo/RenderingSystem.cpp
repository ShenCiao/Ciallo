#include "pch.hpp"
#include "RenderingSystem.h"

#include <glm/gtc/type_ptr.hpp>

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
	glUseProgram(ArticulatedLine.Program);
	glm::mat4 mvp = drawing->GetViewProjMatrix();
	glUniformMatrix4fv(0, 1, GL_FALSE, glm::value_ptr(mvp));

	for(auto& s : drawing->Strokes)
	{
		int count = s->Position.size();
		if(count == 1)
		{
			Geom::Point p = s->Position[0];
			float w = s->Width[0];
			Geom::Point paddedPos = { p.x() + 0.01f * w, p.y() };

			s->Position.push_back(paddedPos);
			s->Width.push_back(w);
			s->UploadPositionData();
			s->UploadWidthData();

			s->Position.pop_back();
			s->Width.pop_back();

			count += 1;
		}
		glBindVertexArray(s->VertexArray);
		glDrawArrays(GL_LINE_STRIP, 0, count);
	}

	glUseProgram(0);
	glBindFramebuffer(GL_FRAMEBUFFER, 0);
}
