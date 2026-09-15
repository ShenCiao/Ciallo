using Godot;
using R3;

namespace Ciallo.GuiControl;

/// <summary>
/// Draws the background column grid in the track area.
/// </summary>
[Tool]
public partial class BackgroundGrid : Control
{
    private const float EditorPreviewPixelsPerFrame = 32f;
    private const float EditorPreviewScrollOffsetFrame = -5f;
    private const int EditorPreviewPlaybackStart = 0;
    private const int EditorPreviewPlaybackEnd = 24;

    // ── Tunable exports ──────────────────────────────────────────────────────
    /// <summary>Draw a major line every N minor-tick frames.</summary>
    [Export] public int MajorColumnInterval { get; set; } = 5;
    /// <summary>Minimum pixel gap between drawn column lines.</summary>
    [Export] public float MinColumnSpacingPx { get; set; } = 6f;

    // ── Private state ────────────────────────────────────────────────────────
    private float _pixelsPerFrame = 20f;
    private float _scrollOffset;
    private ReactiveProperty<int> _playbackStart;
    private ReactiveProperty<int> _playbackEnd;

    public Color ColumnLineColor { get; set; }
    public Color MajorColumnLineColor { get; set; }
    public Color OutOfPlaybackColumnLineColor { get; set; }

    public override void _Ready()
    {
        if (Engine.IsEditorHint())
        {
            _pixelsPerFrame = EditorPreviewPixelsPerFrame;
            _scrollOffset = EditorPreviewScrollOffsetFrame * EditorPreviewPixelsPerFrame;
        }
        InitTheme();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationThemeChanged)
            InitTheme();
    }


    public void InitTheme()
    {
        MajorColumnLineColor = new(0.4f, 0.4f, 0.4f, 1f);
        ColumnLineColor = new(MajorColumnLineColor) { A = 0.5f };
        OutOfPlaybackColumnLineColor = new(MajorColumnLineColor) { A = 0.2f };
    }
    // ── Setup ────────────────────────────────────────────────────────────────

    public void Observe(ReactiveProperty<float> pixelsPerFrame, ReactiveProperty<float> scrollOffsetFrame)
    {
        // Recompute both caches together so _scrollOffset (pixels) is always in sync with _pixelsPerFrame.
        pixelsPerFrame.CombineLatest(scrollOffsetFrame, (ppf, sof) => (ppf, sof * ppf))
            .Subscribe(t =>
            {
                _pixelsPerFrame = t.ppf;
                _scrollOffset = t.Item2;
                QueueRedraw();
            }).AddTo(this);
    }

    public void BindPlaybackRange(ReactiveProperty<int> playbackStart, ReactiveProperty<int> playbackEnd)
    {
        _playbackStart = playbackStart;
        _playbackEnd = playbackEnd;
        playbackStart.Subscribe(_ => QueueRedraw()).AddTo(this);
        playbackEnd.Subscribe(_ => QueueRedraw()).AddTo(this);
    }

    private bool TryGetPlaybackRange(out int playbackStart, out int playbackEnd)
    {
        if (_playbackStart != null && _playbackEnd != null)
        {
            playbackStart = _playbackStart.Value;
            playbackEnd = _playbackEnd.Value;
            return true;
        }

        if (Engine.IsEditorHint())
        {
            playbackStart = EditorPreviewPlaybackStart;
            playbackEnd = EditorPreviewPlaybackEnd;
            return true;
        }

        playbackStart = 0;
        playbackEnd = 0;
        return false;
    }

    // ── Draw ─────────────────────────────────────────────────────────────────

    public override void _Draw()
    {
        if (_pixelsPerFrame <= 0f) return;

        float w = Size.X;
        float h = Size.Y;

        // Choose step so columns are at least MinColumnSpacingPx apart
        int step = 1;
        while (_pixelsPerFrame * step < MinColumnSpacingPx) step *= step < 5 ? 5 : 2;

        // Frame 0 is at virtual x = 0, matching TimelineRuler's coordinate origin.
        var visibleFrames = TimelineFrameGeometry.VisibleFrameRange(w, _pixelsPerFrame, _scrollOffset);
        int startFrame = visibleFrames.Start;
        int endFrame = visibleFrames.End;
        bool hasPlaybackRange = TryGetPlaybackRange(out int playbackStart, out int playbackEnd);

        for (int frame = startFrame; frame <= endFrame; frame++)
        {
            if (frame != 0 && frame % step != 0) continue;

            float x = TimelineFrameGeometry.FrameToX(frame, _pixelsPerFrame, _scrollOffset);
            if (x < -_pixelsPerFrame || x > w + _pixelsPerFrame) continue;

            bool isMajor = frame == 0 || frame % (step * MajorColumnInterval) == 0;
            bool inRange = hasPlaybackRange &&
                TimelineFrameGeometry.IsInPlaybackRange(frame, playbackStart, playbackEnd);
            Color lineColor = inRange
                ? isMajor ? MajorColumnLineColor : ColumnLineColor
                : OutOfPlaybackColumnLineColor;
            DrawLine(new Vector2(x, 0f), new Vector2(x, h), lineColor);
        }
    }
}
