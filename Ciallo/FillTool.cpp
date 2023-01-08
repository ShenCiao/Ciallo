#include "pch.hpp"
// #include "FillTool.h"
//
// #include "Stroke.h"
// #include "CanvasPanel.h"
// #include "RenderingSystem.h"
//
// void FillTool::PadRim()
// {
// 	auto& arr = Canvas->ActiveDrawing->ArrangementSystem;
// 	glm::vec2 size = Canvas->ActiveDrawing->GetWorldSize();
//
// 	Rim = std::make_unique<Stroke>();
// 	Geom::Polyline line = {{0.0f, 0.0f}, {size.x, 0.0f}, size, {0.0f, size.y}, {0.0f, 0.0f}};
// 	Rim->Position = line;
// 	arr.AddOrUpdate(Rim.get());
// }
//
// void FillTool::RemoveRim()
// {
// 	auto& arr = Canvas->ActiveDrawing->ArrangementSystem;
// 	if (Rim)
// 	{
// 		arr.Remove(Rim.get());
// 		Rim.release();
// 	}
// }
//
// void FillTool::ClickOrDragStart()
// {
// 	// Changed part: Thickness, Label, remove Arrangement
// 	auto s = std::make_unique<Stroke>();
// 	// s->Brush = ActiveBrush;
// 	s->Position = {Canvas->MousePosOnDrawing};
// 	s->Thickness = 0.0003f;
// 	s->PolygonColor = PolygonColor;
// 	s->UpdateBuffers();
// 	Canvas->ActiveDrawing->ArrangementSystem.AddOrUpdateQuery(s.get());
// 	Canvas->ActiveDrawing->Labels.push_back(std::move(s));
// 	LastSampleDuration = chrono::duration<float, std::milli>::zero();
// }
//
// void FillTool::Dragging()
// {
// 	// Changed part: Thickness, Label, delta threshold, SampleInterval
// 	glm::vec2 delta = glm::abs(glm::vec2(ImGui::GetMousePos() - LastSampleMousePos));
//
// 	if (DraggingDuration - LastSampleDuration > SampleInterval && delta.x + delta.y >= 6.0f)
// 	{
// 		auto& s = Canvas->ActiveDrawing->Labels.back();
//
// 		glm::vec2 pos = Canvas->MousePosOnDrawing;
// 		s->Position.push_back(pos);
// 		s->UpdateBuffers();
// 		Canvas->ActiveDrawing->ArrangementSystem.AddOrUpdateQuery(s.get());
// 		LastSampleMousePos = ImGui::GetMousePos();
// 		LastSampleDuration = DraggingDuration;
// 	}
// }
//
// void FillTool::Activate()
// {
// 	auto& vis = Canvas->ActiveDrawing->ArrangementSystem.Visibility;
// 	auto& arr = Canvas->ActiveDrawing->ArrangementSystem.Arrangement;
//
// 	vis.attach(arr);
// }
//
// void FillTool::Deactivate()
// {
// 	auto& vis = Canvas->ActiveDrawing->ArrangementSystem.Visibility;
//
// 	vis.detach();
// }
//
// void FillTool::DrawProperties()
// {
// 	ImGui::ColorPicker4("Polygon Color", glm::value_ptr(PolygonColor));
// }
//
// void FillTool::Hovering()
// {
// 	if (ImGui::IsKeyDown(ImGuiKey_LeftAlt))
// 	{
// 		if (!Rim) PadRim();
// 	}
//
// 	if (ImGui::IsKeyReleased(ImGuiKey_LeftAlt))
// 	{
// 		if (Rim) RemoveRim();
// 	}
//
// 	// In vis mode
// 	if (ImGui::IsKeyDown(ImGuiKey_LeftAlt))
// 	{
// 		glBindFramebuffer(GL_FRAMEBUFFER, Canvas->OverlayFrameBuffer);
// 		glClearColor(0.0f, 0.0f, 0.0f, 0.0f);
// 		glClear(GL_COLOR_BUFFER_BIT);
// 		glEnable(GL_BLEND);
// 		glBlendFunc(GL_ONE, GL_ZERO);
// 		glEnable(GL_STENCIL_TEST);
// 		glDisable(GL_DEPTH_TEST);
//
// 		auto& arr = Canvas->ActiveDrawing->ArrangementSystem;
// 		auto polygon = arr.PointQueryVisibility(Canvas->MousePosOnDrawing);
//
// 		glUseProgram(RenderingSystem::Polygon->Program);
// 		glm::mat4 mvp = Canvas->ActiveDrawing->GetViewProjMatrix();
// 		glUniformMatrix4fv(0, 1, GL_FALSE, glm::value_ptr(mvp)); // mvp
// 		glUniform4fv(1, 1, glm::value_ptr(PolygonColor)); // color
// 		ColorFace face{ {polygon} };
// 		face.GenUploadBuffers();
// 		face.DrawCall();
//
// 		glDisable(GL_STENCIL_TEST);
//
// 		Stroke s{};
// 		s.Position = polygon;
// 		s.ThicknessOffset = std::vector<float>(polygon.size(), 0.001f);
// 		s.UpdateBuffers();
// 		glUseProgram(RenderingSystem::ArticulatedLine->Program());
// 		glUniformMatrix4fv(0, 1, GL_FALSE, glm::value_ptr(mvp)); // mvp
// 		glUniform4f(1, 0.9f, 0.15f, 0.3f, 0.5f); // color
// 		glBindVertexArray(s.VertexArray);
// 		glDrawArrays(GL_LINE_LOOP, 0, s.Position.size());
//
// 		glBindFramebuffer(GL_FRAMEBUFFER, 0);
//
// 		glBlendFunc(GL_SRC_COLOR, GL_ONE_MINUS_SRC_ALPHA);
// 		return;
// 	}
//
// 	// In fill preview mode
// 	else
// 	{
// 		glBindFramebuffer(GL_FRAMEBUFFER, Canvas->OverlayFrameBuffer);
// 		glClearColor(0.0f, 0.0f, 0.0f, 0.0f);
// 		glClear(GL_COLOR_BUFFER_BIT);
// 		glEnable(GL_BLEND);
// 		glBlendFunc(GL_ONE, GL_ZERO);
// 		glEnable(GL_STENCIL_TEST);
// 		glDisable(GL_DEPTH_TEST);
//
// 		auto& arr = Canvas->ActiveDrawing->ArrangementSystem;
// 		auto polygon = arr.PointQuery(Canvas->MousePosOnDrawing);
// 		if(polygon.size() > 0)
// 		{
// 			glUseProgram(RenderingSystem::Polygon->Program);
// 			glm::mat4 mvp = Canvas->ActiveDrawing->GetViewProjMatrix();
// 			glUniformMatrix4fv(0, 1, GL_FALSE, glm::value_ptr(mvp)); // mvp
// 			glUniform4fv(1, 1, glm::value_ptr(PolygonColor)); // color
// 			ColorFace face{ polygon };
// 			face.GenUploadBuffers();
// 			face.DrawCall();
//
// 			glBindFramebuffer(GL_FRAMEBUFFER, 0);
// 			glBlendFunc(GL_SRC_COLOR, GL_ONE_MINUS_SRC_ALPHA);
// 		}
// 	}
//
// }
