#include "pch.hpp"
#include "Canvas.h"

void Canvas::DrawUI()
{
	ImGui::Begin("Canvas");
	ImGui::Image(ImTextureID(Image.ColorTexture), Image.Size());
	ImGui::End();
}

void Canvas::GenRenderTarget()
{
	auto size = Viewport.GetSizePixel(Dpi);
	Image = RenderableTexture(size.x, size.y);
}
