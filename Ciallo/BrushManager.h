#pragma once

#include "Stroke.h"
#include "Viewport.h"

class BrushManager
{
	entt::entity* TargetBrushE;
	Stroke PreviewStroke;
	glm::vec4 PreviewBackgroundColor = {1.0f, 1.0f, 1.0f, 1.0f};
	int SegmentCount = 32;
public:
	entt::entity EditorActiveBrushE = entt::null;
	std::vector<entt::entity> Brushes;
	Viewport PreviewPort;

	BrushManager();

	void GenPreviewStroke(int nSegment);
	void RenderAllPreview();
	void SetContext() const;
	void RenderPreview(entt::entity brushE);
	void DrawUI();
	void OpenBrushEditor(entt::entity* brushE);
	void ExportDemo();

	template<class Archive>
	void serialize(Archive& archive) {
		archive(EditorActiveBrushE, PreviewBackgroundColor, SegmentCount, Brushes);
	}
};

