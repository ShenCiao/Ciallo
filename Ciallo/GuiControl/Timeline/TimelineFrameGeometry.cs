using Godot;

namespace Ciallo.GuiControl;

internal static class TimelineFrameGeometry
{
    public static float FrameToX(int frame, float pixelsPerFrame, float scrollOffset) =>
        frame * pixelsPerFrame - scrollOffset;

    public static int XToFrameFloor(float x, float pixelsPerFrame, float scrollOffset) =>
        pixelsPerFrame > 0f ? Mathf.FloorToInt((x + scrollOffset) / pixelsPerFrame) : 0;

    public static int XToFrameRounded(float x, float pixelsPerFrame, float scrollOffset) =>
        pixelsPerFrame > 0f ? Mathf.RoundToInt((x + scrollOffset) / pixelsPerFrame) : 0;

    /// <summary>
    /// Playback range membership. The range is half-open: <paramref name="playbackEnd"/> is the
    /// first frame past the range, so the last played frame is <c>playbackEnd - 1</c>.
    /// </summary>
    public static bool IsInPlaybackRange(int frame, int playbackStart, int playbackEnd) =>
        frame >= playbackStart && frame < playbackEnd;

    /// <summary>
    /// Returns the frame whose cell contains <paramref name="x"/>, but only when x also lands in
    /// the leading <paramref name="spanWidth"/> pixels of that cell; null otherwise. Models a
    /// widget anchored to a frame's left edge and narrower than the frame itself.
    /// <para>
    /// Requires <paramref name="spanWidth"/> &lt;= <paramref name="pixelsPerFrame"/>. A wider span
    /// would reach into the following frame's cell, which this cell-based lookup cannot resolve.
    /// </para>
    /// </summary>
    public static int? XToFrameInLeadingSpan(
        float x,
        float pixelsPerFrame,
        float scrollOffset,
        float spanWidth)
    {
        if (pixelsPerFrame <= 0f || spanWidth <= 0f) return null;

        int frame = XToFrameFloor(x, pixelsPerFrame, scrollOffset);
        float offsetInCell = x - FrameToX(frame, pixelsPerFrame, scrollOffset);
        return offsetInCell < spanWidth ? frame : null;
    }

    public static (int Start, int End) VisibleFrameRange(
        float width,
        float pixelsPerFrame,
        float scrollOffset,
        int leadingBufferFrames = 1,
        int trailingBufferFrames = 2)
    {
        if (pixelsPerFrame <= 0f)
            return (0, 0);

        int start = Mathf.FloorToInt(scrollOffset / pixelsPerFrame) - leadingBufferFrames;
        int end = Mathf.FloorToInt((scrollOffset + width) / pixelsPerFrame) + trailingBufferFrames;
        return (start, end);
    }
}
