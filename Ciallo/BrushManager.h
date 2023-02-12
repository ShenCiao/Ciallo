#pragma once

#include "Stroke.h"
#include "Viewport.h"

class BrushManager
{
	entt::entity EditorActiveBrushE = entt::null;
	entt::entity* TargetBrushE;
	Stroke PreviewStroke;
public:
	std::vector<entt::entity> Brushes;
	Viewport PreviewPort;

	BrushManager();

	void GenPreviewStroke();
	void RenderAllPreview();
	void SetContext() const;
	void RenderPreview(entt::entity brushE);
	void DrawUI();
	void OpenBrushEditor(entt::entity* brushE);
	void ExportDemo();
};

