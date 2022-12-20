#pragma once

#include "Stroke.h"
#include "Viewport.h"

class BrushManager
{
public:
	std::vector<entt::entity> Brushes;
	Stroke PreviewStroke;
	Viewport PreviewPort;

	BrushManager();

	void GenPreviewStroke();
	void RenderAllPreview();
	void SetContext() const;
	void RenderPreview(entt::entity brushE);
	void Draw();
	void OutputPreview();
};

