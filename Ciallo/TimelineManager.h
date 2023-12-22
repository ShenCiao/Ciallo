#pragma once
class TimelineManager
{
	int CurrentFrame = 1;
	int StartFrame = 1;
	int EndFrame = 32;
	std::vector<entt::entity> DrawingEs;
	std::vector<int> KeyFrames;
public:
	TimelineManager();
	entt::entity GenKeyFrame(int keyNumber);
	void RemoveKeyFrame(int keyNumber);
	entt::entity GetCurrentDrawing();
	entt::entity GetRenderDrawing();
	entt::entity GetPreviousDrawing();
	entt::entity GetDrawing(int keyNumber);
	void DrawUI();
};