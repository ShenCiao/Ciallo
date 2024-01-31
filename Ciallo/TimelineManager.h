#pragma once
class TimelineManager
{
public:
	int CurrentFrame = 1;
	int StartFrame = 1;
	int EndFrame = 64;
	std::vector<entt::entity> DrawingEs;
	std::vector<int> KeyFrames;

	int ExportingIndex = -1;

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
	void ExportAllFrames();

	template<class Archive>
	void serialize(Archive& archive) {
		archive(CurrentFrame, StartFrame, EndFrame, DrawingEs, KeyFrames);
	}
};