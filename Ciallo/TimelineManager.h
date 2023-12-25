#pragma once
class TimelineManager
{
public:
	int CurrentFrame = 1;
	int StartFrame = 1;
	int EndFrame = 32;
	std::vector<entt::entity> DrawingEs;
	std::vector<int> KeyFrames;

	TimelineManager();
	entt::entity GenKeyFrame(int keyNumber);
	void RemoveKeyFrame(int keyNumber);
	entt::entity GetCurrentDrawing();
	entt::entity GetRenderDrawing();
	entt::entity GetPreviousDrawing();
	entt::entity GetDrawing(int keyNumber);

	std::vector<entt::entity> CopiedStrokeEs;
	void CopyFillMarker(entt::entity drawingE);
	void PasteFillMarker(entt::entity drawingE);

	void DrawUI();
	void Clear();
};