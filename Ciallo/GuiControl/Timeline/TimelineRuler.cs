using System.Collections.Immutable;
using Ciallo.Command;
using Ciallo.Data;
using Frent;
using Godot;
using R3;

namespace Ciallo.GuiControl;

/// <summary>
/// Draws frame-number ruler ticks and draggable playback-range handles.
/// </summary>
[Tool]
public partial class TimelineRuler : Control
{
    private const float EditorPreviewPixelsPerFrame = 32f;
    private const float EditorPreviewScrollOffsetFrame = -5f;
    private const int EditorPreviewPlaybackStart = 0;
    private const int EditorPreviewPlaybackEnd = 24;

    #region Export

    [Export]
    public int MajorTickHeight
    {
        get;
        set
        {
            field = value;
            QueueRedraw();
        }
    } = 16;

    [Export]
    public int MinorTickHeight
    {
        get;
        set
        {
            field = value;
            QueueRedraw();
        }
    } = 6;

    /// <summary>Minimum pixel gap between displayed labels.</summary>
    [Export]
    public float MinLabelSpacingPx
    {
        get;
        set
        {
            field = value;
            QueueRedraw();
        }
    } = 40f;

    /// <summary>Minimum pixel gap between tick marks.</summary>
    [Export]
    public float MinTickSpacingPx
    {
        get;
        set
        {
            field = value;
            QueueRedraw();
        }
    } = 8f;

    [Export]
    public Color TickColor
    {
        get;
        set
        {
            field = value;
            QueueRedraw();
        }
    } = new Color(0.65f, 0.65f, 0.65f);

    /// <summary>Pixel height of the seconds band drawn above the frame-tick band.</summary>
    [Export]
    public float SecondsBandHeight
    {
        get;
        set
        {
            field = value;
            QueueRedraw();
        }
    } = 18f;

    [Export] public TimelineRulerRightClickMenu RightClickMenu { get; set; }

    #endregion

    // ── Private state ────────────────────────────────────────────────────────
    private float _pixelsPerFrame = 20f;
    private float _scrollOffset;
    private float _fps = 24f;
    private ReactiveProperty<int> _currentFrame;
    private ReactiveProperty<int> _playbackStart;
    private ReactiveProperty<int> _playbackEnd;
    private SelectionManager _selectionManager;
    private Playhead _playhead;
    private readonly ReactiveProperty<bool> _isScrubbing = new(false);
    public ReadOnlyReactiveProperty<bool> IsScrubbing => _isScrubbing;

    private enum DragMode { None, PendingFrame, Frame, StartHandle, EndHandle }

    private const float DragStartDistance = 3f;
    private DragMode _dragMode = DragMode.None;
    private int? _hoverFrame;
    private Vector2 _dragStartPosition;
    private int _frameAtDragStart;
    private int _playbackStartAtDragStart;
    private int _playbackEndAtDragStart;
    private int? _rightClickIndicatorFrame;

    #region Theme

    public int LabelFontSize;
    public Color LabelColor;
    public Color PlaybackBackgroundColor;
    public Color OutOfPlaybackBackgroundColor;
    public Color OutOfPlaybackLabelColor;
    public Color OutOfPlaybackTickColor;
    public Color HintDotColor;

    public void InitTheme()
    {
        StyleBoxFlat styleBox;
        styleBox = (StyleBoxFlat)GetThemeStylebox("normal", "Button");
        PlaybackBackgroundColor = styleBox.BgColor;
        styleBox = (StyleBoxFlat)GetThemeStylebox("disabled", "Button");
        OutOfPlaybackBackgroundColor = styleBox.BgColor;
        LabelColor = GetThemeColor("font_color", "Label");
        LabelFontSize = (int)(0.8 * GetThemeFontSize("font_size", "Label"));
        OutOfPlaybackLabelColor = GetThemeColor("font_disabled_color", "Button");
        OutOfPlaybackTickColor = OutOfPlaybackLabelColor with { A = 0.5f };
        styleBox = (StyleBoxFlat)GetThemeStylebox("hover", "Button");
        HintDotColor = styleBox.BgColor;
    }

    #endregion

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

        if (what == NotificationMouseExit)
        {
            _hoverFrame = null;
            QueueRedraw();
        }
    }

    #region Setup

    /// <summary>Call once from TimelinePanel to wire zoom / scroll.</summary>
    public void Observe(ReactiveProperty<float> pixelsPerFrame, ReactiveProperty<float> scrollOffsetFrame,
        ReactiveProperty<float> fps)
    {
        pixelsPerFrame.CombineLatest(scrollOffsetFrame, (ppf, sof) => (ppf, sof * ppf))
            .Subscribe(t =>
            {
                _pixelsPerFrame = t.ppf;
                _scrollOffset = t.Item2;
                QueueRedraw();
            }).AddTo(this);
        fps.Subscribe(v =>
        {
            _fps = v;
            QueueRedraw();
        }).AddTo(this);
    }

    /// <summary>Wire the current-frame property.</summary>
    public void BindCurrentFrame(ReactiveProperty<int> currentFrame)
    {
        _currentFrame = currentFrame;
    }

    /// <summary>Wire playback-range properties.</summary>
    public void BindPlaybackRange(ReactiveProperty<int> playbackStart, ReactiveProperty<int> playbackEnd)
    {
        _playbackStart = playbackStart;
        _playbackEnd = playbackEnd;
        playbackStart.Subscribe(_ => QueueRedraw()).AddTo(this);
        playbackEnd.Subscribe(_ => QueueRedraw()).AddTo(this);
    }

    /// <summary>Wire the selection manager for primary-layer switching on frame change.</summary>
    public void BindSelectionManager(SelectionManager sm)
    {
        _selectionManager = sm;
    }

    /// <summary>Wire the playhead so frame dragging can preview continuous visual motion.</summary>
    public void BindPlayhead(Playhead playhead)
    {
        _playhead = playhead;
    }

    #endregion

    // ── Coordinate helpers ───────────────────────────────────────────────────

    /// <summary>Ruler-local pixel X of a frame's left edge. Frame 0 is the virtual origin.</summary>
    private float FrameToX(int frame) => TimelineFrameGeometry.FrameToX(frame, _pixelsPerFrame, _scrollOffset);

    /// <summary>Frame index whose left edge is at or before ruler-local X. May return 0 or negative.</summary>
    private int XToFrame(float x) => TimelineFrameGeometry.XToFrameFloor(x, _pixelsPerFrame, _scrollOffset);

    private static string FormatFrameLabel(int frame) => frame < 0 ? frame.ToString() : (frame + 1).ToString();

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

    // ── Handle hit-tests ─────────────────────────────────────────────────────

    /// <summary>Horizontal pixel tolerance added on each side of a handle's bounding box.</summary>
    private const float HandleHitTolerance = 2f;

    /// <summary>
    /// Hit zone covers the triangle bounding box with a small horizontal tolerance.
    /// </summary>
    private bool HitStartHandle(Vector2 pos)
    {
        if (_playbackStart == null) return false;
        float startX = FrameToX(_playbackStart.Value);
        return pos.X >= startX - Size.Y - HandleHitTolerance &&
               pos.X <= startX + HandleHitTolerance &&
               pos.Y >= 0f && pos.Y <= Size.Y;
    }

    private bool HitEndHandle(Vector2 pos)
    {
        if (_playbackEnd == null) return false;
        float endX = FrameToX(_playbackEnd.Value);
        return pos.X >= endX - HandleHitTolerance &&
               pos.X <= endX + Size.Y + HandleHitTolerance &&
               pos.Y >= 0f && pos.Y <= Size.Y;
    }

    // ── Drawing ──────────────────────────────────────────────────────────────

    public override void _Draw()
    {
        if (_pixelsPerFrame <= 0f) return;

        float w = Size.X;
        float h = Size.Y;

        // ── Background ────────────────────────────────────────────────────────
        bool hasPlaybackRange = TryGetPlaybackRange(out int playbackStart, out int playbackEnd);
        if (hasPlaybackRange)
        {
            float startX = FrameToX(playbackStart);
            float endX = FrameToX(playbackEnd);

            // Out-of-range left
            if (startX > 0f)
                DrawRect(new Rect2(0f, 0f, Mathf.Min(startX, w), h), OutOfPlaybackBackgroundColor);

            // In-range
            float clampedStart = Mathf.Clamp(startX, 0f, w);
            float clampedEnd = Mathf.Clamp(endX, 0f, w);
            if (clampedEnd > clampedStart)
                DrawRect(new Rect2(clampedStart, 0f, clampedEnd - clampedStart, h), PlaybackBackgroundColor);

            // Out-of-range right
            float rightStart = Mathf.Max(0f, endX);
            if (rightStart < w)
                DrawRect(new Rect2(rightStart, 0f, w - rightStart, h), OutOfPlaybackBackgroundColor);
        }
        else
        {
            DrawRect(new Rect2(0f, 0f, w, h), OutOfPlaybackBackgroundColor);
        }

        var font = GetThemeDefaultFont();
        // Frame 0 is at virtual x=0; include a 1-frame buffer left of the view.
        var visibleFrames = TimelineFrameGeometry.VisibleFrameRange(w, _pixelsPerFrame, _scrollOffset);
        int startFrame = visibleFrames.Start;
        int endFrame = visibleFrames.End;

        // ── Seconds band (top) ───────────────────────────────────────────────
        // Band occupies y ∈ [0, SecondsBandHeight); frame band is below.
        if (_fps > 0f)
        {
            float pxPerSecond = _pixelsPerFrame * _fps;

            // Find the smallest step (in whole seconds) so labels don't overlap.
            int secondStep = 1;
            if (pxPerSecond > 0f)
                while (pxPerSecond * secondStep < MinLabelSpacingPx)
                    secondStep++;

            int startSecond = Mathf.FloorToInt(_scrollOffset / pxPerSecond) - 1;
            int endSecond = Mathf.FloorToInt((_scrollOffset + w) / pxPerSecond) + 2;

            // Separator line between the two bands
            DrawLine(new Vector2(0f, SecondsBandHeight), new Vector2(w, SecondsBandHeight),
                TickColor with { A = 0.4f });

            for (int s = startSecond; s <= endSecond; s++)
            {
                if (s % secondStep != 0) continue;

                // frame for second s: frame 0 = 0 s, frame (s*Fps) = s seconds
                float x = FrameToX((int)(s * _fps));
                if (x < -pxPerSecond || x > w + pxPerSecond) continue;

                int secondFrame = (int)(s * _fps);
                bool inRange = hasPlaybackRange &&
                    TimelineFrameGeometry.IsInPlaybackRange(secondFrame, playbackStart, playbackEnd);
                Color secTickColor = inRange ? TickColor : OutOfPlaybackTickColor;
                Color secLabelColor = inRange ? LabelColor : OutOfPlaybackLabelColor;

                DrawLine(new Vector2(x, 0f), new Vector2(x, SecondsBandHeight), secTickColor);
                DrawString(font, new Vector2(x + 2f, SecondsBandHeight - 2f),
                    $"{s}s", HorizontalAlignment.Left, -1, LabelFontSize, secLabelColor);
            }
        }

        // ── Frame ticks & labels (bottom band) ──────────────────────────────
        // Minor-tick step: smallest interval keeping ticks ≥ MinTickSpacingPx apart.
        int tickStep = 1;
        while (_pixelsPerFrame * tickStep < MinTickSpacingPx) tickStep *= tickStep < 5 ? 5 : 2;

        // Major ticks every 5 frames (or next multiple of 5 that covers tickStep).
        int majorStep = 5;
        while (majorStep < tickStep) majorStep += 5;

        // Labels only when the major-tick pixel gap is roomy enough.
        bool showFrameLabels = _pixelsPerFrame * majorStep >= MinLabelSpacingPx;

        for (int frame = startFrame; frame <= endFrame; frame++)
        {
            bool isTickFrame = frame == 1 || frame % tickStep == 0;
            if (!isTickFrame) continue;

            float x = FrameToX(frame);
            if (x < -_pixelsPerFrame || x > w + _pixelsPerFrame) continue;

            bool isMajor = frame == 0 || frame % majorStep == 0;
            float tickH = isMajor ? MajorTickHeight : MinorTickHeight;

            bool inRange = hasPlaybackRange &&
                TimelineFrameGeometry.IsInPlaybackRange(frame, playbackStart, playbackEnd);
            Color frameTickColor = inRange ? TickColor : OutOfPlaybackTickColor;
            Color frameLabelColor = inRange ? LabelColor : OutOfPlaybackLabelColor;

            DrawLine(new Vector2(x, h - tickH), new Vector2(x, h), frameTickColor);

            // Frame 0 has no visible label — numbering goes …-2, -1, (silent 0), 1, 2…
            if (isMajor && showFrameLabels && frame != 0)
                DrawString(font, new Vector2(x + 2f, h - tickH - 2f),
                        FormatFrameLabel(frame), HorizontalAlignment.Left, -1, LabelFontSize, frameLabelColor);
        }

        // ── Hover / drag hint ────────────────────────────────────────────────
        if (_hoverFrame != null)
        {
            // Center of the hovered frame's pixel slot
            float hx = FrameToX(_hoverFrame.Value) + _pixelsPerFrame * 0.5f;

            // Dot vertically centered within the minor-tick height
            float dotY = h - MinorTickHeight * 0.5f;
            DrawCircle(new Vector2(hx, dotY), 6f, HintDotColor);

            // Frame label in the seconds band, centered on the same X as the dot.
            string hoverLabel = FormatFrameLabel(_hoverFrame.Value);
            float labelW = font.GetStringSize(hoverLabel, HorizontalAlignment.Left, -1, LabelFontSize).X;
            DrawString(font, new Vector2(hx - labelW * 0.5f, SecondsBandHeight - 2f),
                hoverLabel, HorizontalAlignment.Left, -1, LabelFontSize, LabelColor);
        }

        if (_rightClickIndicatorFrame != null)
        {
            float ix = FrameToX(_rightClickIndicatorFrame.Value);
            if (ix >= 0f && ix <= w)
                DrawLine(new Vector2(ix, 0f), new Vector2(ix, h),
                    new Color(1f, 1f, 1f, 0.75f), width: 1f);
        }
    }

    // ── Input ─────────────────────────────────────────────────────────────────

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true } rightBtn)
        {
            if (RightClickMenu == null) return;

            _rightClickIndicatorFrame = XToFrame(rightBtn.Position.X);
            QueueRedraw();
            RightClickMenu.PopupHide += OnRightClickMenuClosed;
            RightClickMenu.Popup(_rightClickIndicatorFrame.Value);
            AcceptEvent();
        }
        else if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left } btn)
        {
            if (btn.Pressed)
            {
                // Priority: handles first, then playhead
                if (HitStartHandle(btn.Position))
                {
                    _dragMode = DragMode.StartHandle;
                    _playbackStartAtDragStart = _playbackStart.Value;
                    _frameAtDragStart = _currentFrame?.Value ?? 0;
                }
                else if (HitEndHandle(btn.Position))
                {
                    _dragMode = DragMode.EndHandle;
                    _playbackEndAtDragStart = _playbackEnd.Value;
                    _frameAtDragStart = _currentFrame?.Value ?? 0;
                }
                else if (_currentFrame != null)
                {
                    _dragMode = DragMode.PendingFrame;
                    _dragStartPosition = btn.Position;
                    _frameAtDragStart = _currentFrame.Value;
                }

                if (_dragMode != DragMode.None) AcceptEvent();
            }
            else
            {
                if (_dragMode == DragMode.PendingFrame)
                {
                    CommitFrameChange(FrameFromX(btn.Position.X));
                }
                else if (_dragMode == DragMode.Frame)
                {
                    CommitFrameChange(_currentFrame.Value);
                    _playhead?.ClearPreview();
                    _isScrubbing.Value = false;
                }
                else if (_dragMode == DragMode.StartHandle)
                {
                    var cmd = new CommandBuilder("Set Playback Start")
                        .SetProperty(_playbackStart, _playbackStartAtDragStart, _playbackStart.Value);
                    if (_currentFrame != null && _currentFrame.Value != _frameAtDragStart)
                    {
                        cmd.SetProperty(_currentFrame, _frameAtDragStart, _currentFrame.Value);
                        var newLayers = ResolveLayersAfterFrameChange(_currentFrame.Value);
                        if (_selectionManager.NeedsTimelineSelectionCommit(newLayers))
                            cmd.SetTarget(newLayers[0]).SelectLayers(layers: newLayers);
                    }
                    cmd.CommitOpenSequence();
                }
                else if (_dragMode == DragMode.EndHandle)
                {
                    var cmd = new CommandBuilder("Set Playback End")
                        .SetProperty(_playbackEnd, _playbackEndAtDragStart, _playbackEnd.Value);
                    if (_currentFrame != null && _currentFrame.Value != _frameAtDragStart)
                    {
                        cmd.SetProperty(_currentFrame, _frameAtDragStart, _currentFrame.Value);
                        var newLayers = ResolveLayersAfterFrameChange(_currentFrame.Value);
                        if (_selectionManager.NeedsTimelineSelectionCommit(newLayers))
                            cmd.SetTarget(newLayers[0]).SelectLayers(layers: newLayers);
                    }
                    cmd.CommitOpenSequence();
                }
                _dragMode = DragMode.None;
            }
        }
        else if (@event is InputEventMouseMotion motion)
        {
            _hoverFrame = XToFrame(motion.Position.X);
            QueueRedraw();
            if (_dragMode != DragMode.None)
            {
                switch (_dragMode)
                {
                    case DragMode.PendingFrame:
                        if (motion.Position.DistanceTo(_dragStartPosition) >= DragStartDistance)
                        {
                            _dragMode = DragMode.Frame;
                            _isScrubbing.Value = true;
                            _playhead?.PreviewAtRulerCenterX(DragPreviewCenterXFromX(motion.Position.X));
                            _currentFrame.Value = FrameFromX(motion.Position.X);
                        }
                        break;
                    case DragMode.Frame:
                        _playhead?.PreviewAtRulerCenterX(DragPreviewCenterXFromX(motion.Position.X));
                        _currentFrame.Value = FrameFromX(motion.Position.X);
                        break;
                    case DragMode.StartHandle:
                        int newStart = StartFromX(motion.Position.X);
                        _playbackStart.Value = newStart;
                        if (_currentFrame?.Value < newStart)
                            _currentFrame.Value = newStart;
                        break;
                    case DragMode.EndHandle:
                        int newEnd = EndFromX(motion.Position.X);
                        _playbackEnd.Value = newEnd;
                        if (_currentFrame?.Value >= newEnd)
                            _currentFrame.Value = newEnd - 1;
                        break;
                }
                AcceptEvent();
            }
        }
    }

    private void OnRightClickMenuClosed()
    {
        RightClickMenu.PopupHide -= OnRightClickMenuClosed;
        _rightClickIndicatorFrame = null;
        QueueRedraw();
    }

    // ── Cursor ────────────────────────────────────────────────────────────────

    public override int _GetCursorShape(Vector2 atPosition)
    {
        if (HitStartHandle(atPosition) || HitEndHandle(atPosition))
            return (int)CursorShape.Hsize;
        if (_currentFrame != null && _playbackStart != null && _playbackEnd != null)
        {
            int frame = XToFrame(atPosition.X);
            if (TimelineFrameGeometry.IsInPlaybackRange(frame, _playbackStart.Value, _playbackEnd.Value))
                return (int)CursorShape.PointingHand;
        }
        return (int)CursorShape.Arrow;
    }

    // ── Mutation helpers with constraints ────────────────────────────────

    /// <summary>
    /// Compute playhead frame from ruler-local X, clamped to [PlaybackStart, PlaybackEnd).
    /// </summary>
    private int FrameFromX(float localX)
    {
        int frame = XToFrame(localX);
        if (_playbackStart != null)
            frame = Mathf.Clamp(frame, _playbackStart.Value, _playbackEnd.Value - 1);
        return frame;
    }

    private float DragPreviewCenterXFromX(float localX)
    {
        if (_playbackStart == null || _playbackEnd == null)
            return localX;

        float halfFrameWidth = _pixelsPerFrame * 0.5f;
        float minX = FrameToX(_playbackStart.Value) + halfFrameWidth;
        float maxX = FrameToX(_playbackEnd.Value - 1) + halfFrameWidth;
        return Mathf.Clamp(localX, minX, maxX);
    }

    private void CommitFrameChange(int newFrame)
    {
        if (_currentFrame.Value != newFrame)
            _currentFrame.Value = newFrame;

        var cmd = new CommandBuilder("Set Current Frame")
            .SetProperty(_currentFrame, _frameAtDragStart, _currentFrame.Value);
        if (_currentFrame.Value != _frameAtDragStart)
        {
            var newLayers = ResolveLayersAfterFrameChange(_currentFrame.Value);
            if (_selectionManager.NeedsTimelineSelectionCommit(newLayers))
                cmd.SetTarget(newLayers[0]).SelectLayers(layers: newLayers);
        }
        cmd.CommitToLatest();
    }

    /// <summary>
    /// Compute new PlaybackStart from ruler-local X, clamped to [0, PlaybackEnd-1].
    /// </summary>
    private int StartFromX(float localX) =>
        Mathf.Min(XToFrame(localX), _playbackEnd.Value - 1);

    /// <summary>
    /// Compute new PlaybackEnd from ruler-local X, clamped to [PlaybackStart+1, ∞).
    /// </summary>
    private int EndFromX(float localX) =>
        Mathf.Max(XToFrame(localX), _playbackStart.Value + 1);

    // ── Selected-layer switch on frame change ─────────────────────────────────

    /// <summary>Delegates to <see cref="SelectionManager.ResolveLayersForTimelineFrameSelection"/>.</summary>
    private ImmutableArray<Entity> ResolveLayersAfterFrameChange(int frame) =>
        _selectionManager.ResolveLayersForTimelineFrameSelection(frame);
}
