using Ciallo.Command;
using Ciallo.Data;
using Frent;
using Godot;
using ObservableCollections;
using R3;

namespace Ciallo.GuiControl;

/// <summary>
/// Draws the dope-sheet exposure track for one CelFolder in the right panel of its
/// <see cref="TrackRow"/> inside <see cref="TrackTree"/>.
/// <list type="bullet">
///   <item>Lives as a normal (non-TopLevel) child of the <see cref="TrackRow"/> HSplitContainer
///         and fills the right panel via <see cref="SizeFlags.ExpandFill"/>.</item>
///   <item>Every exposure key draws a cel button; each cel exposure's button carries an outgoing
///         exposure span that spans until the next exposure.</item>
/// </list>
/// Call <see cref="Observe"/> and <see cref="Bind"/> once after adding to the scene.
/// </summary>
[Tool]
public partial class CelTrack : Control
{
    // ── Tunable ──────────────────────────────────────────────────────────────
    /// <summary>
    /// Fraction of one frame cell a cel button occupies. Must stay ≤ 1 so a button never spills into
    /// the next frame's cell: <see cref="CelButtonFrameAt"/> hit-tests on that invariant.
    /// </summary>
    public float CelButtonWidthRatio = 0.5f;
    public float MaxCelButtonWidth = 24f;
    public float CelButtonWidth => Mathf.Min(_pixelsPerFrame * CelButtonWidthRatio, MaxCelButtonWidth);

    public float SpanArrowHeadLength = 7f;
    public float SpanArrowHeadHalfWidth = 4f;
    public float LabelPad = 3f;

    /// <summary>Scales shaft width and head size of the exposure span being dragged.</summary>
    private const float DraggedSpanArrowThickness = 2f;

    private const string ChangeExposureDurationCommandName = "Change Cel Exposure Duration";

    // ── State ─────────────────────────────────────────────────────────────────
    private float _pixelsPerFrame;
    private float _scrollOffset;
    private int _playbackStart;
    private int _playbackEnd;
    private ObservableSortedList<int, Entity> _exposures;
    private bool _isSelected;

    // ── Interaction state ─────────────────────────────────────────────────────
    private const float DragThreshold = 3f;
    private const float BlankExposureStrokeWidth = 6f;
    /// <summary>Press X of whichever gesture is active; both drags measure their threshold from it.</summary>
    private float _pressStartX;

    private int? _hoveredFrame;
    private int? _pressedFrame;
    private bool _isCelButtonDragging;
    private int? _celButtonDragSourceFrame;
    private int? _celButtonDragTargetFrame;

    private ExposureSpan? _pressedSpan;
    private bool _isSpanDragging;
    private int? _spanDragTargetFrame;

    // ── Right-click indicator ─────────────────────────────────────────────────
    private int? _rightClickIndicatorFrame;

    // ── Entity references (set by Bind) ───────────────────────────────────────
    private Entity _celFolderEntity;
    private SelectionManager _selectionManager;
    public ReactiveProperty<int> CurrentFrame => _selectionManager.CurrentFrame;

    // ── Right-click menu ──────────────────────────────────────────────────────
    public CelTrackRightClickMenu RightClickMenu { get; set; }

    // ── Theme ─────────────────────────────────────────────────────────────────
    public Color CelButtonNormalColor;
    public Color CelButtonHoverColor;
    public Color CelButtonPressedColor;
    public Color LabelColor;
    public Color ExposureSpanColor;
    public Font LabelFont;
    public int LabelFontSize;
    /// <summary>Hardcoded orange, identical to the Playhead's border color.</summary>
    public Color PlayheadAccentColor = new(207 / 255f, 167 / 255f, 106 / 255f, 1f);

    // ── Constructor ───────────────────────────────────────────────────────────

    public CelTrack()
    {
        MouseFilter = MouseFilterEnum.Pass;
        ClipContents = true;
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.ExpandFill;
    }

    // ── Theme init ────────────────────────────────────────────────────────────

    private void InitTheme()
    {
        var normalStyleBox = (StyleBoxFlat)GetThemeStylebox("normal", "Button");
        CelButtonNormalColor = normalStyleBox.BgColor;
        var hoverStyleBox = (StyleBoxFlat)GetThemeStylebox("hover", "Button");
        CelButtonHoverColor = hoverStyleBox.BgColor;
        var pressedStyleBox = (StyleBoxFlat)GetThemeStylebox("pressed", "Button");
        CelButtonPressedColor = pressedStyleBox.BgColor;
        LabelColor = GetThemeColor("font_color", "Button");
        ExposureSpanColor = LabelColor with { A = 0.8f };
        LabelFont = GetThemeFont("font", "Button");
        LabelFontSize = (int)(GetThemeFontSize("font_size", "Button") * 0.8f);
    }

    public override void _EnterTree() => InitTheme();

    public override void _Notification(int what)
    {
        if (what == NotificationThemeChanged)
        {
            InitTheme();
            QueueRedraw();
        }
        else if (what == NotificationMouseExit)
        {
            _hoveredFrame = null;
            QueueRedraw();
        }
    }

    // ── Setup ─────────────────────────────────────────────────────────────────

    public void Observe(
        ReactiveProperty<float> pixelsPerFrame,
        ReactiveProperty<float> scrollOffsetFrame,
        ReactiveProperty<int> playbackStart,
        ReactiveProperty<int> playbackEnd,
        CompositeDisposable subs)
    {
        pixelsPerFrame.CombineLatest(scrollOffsetFrame, (ppf, sof) => (ppf, sof * ppf))
            .Subscribe(t =>
            {
                _pixelsPerFrame = t.ppf;
                _scrollOffset = t.Item2;
                QueueRedraw();
            }).AddTo(subs);
        playbackStart.Subscribe(v =>
        {
            _playbackStart = v;
            QueueRedraw();
        }).AddTo(subs);
        playbackEnd.Subscribe(v =>
        {
            _playbackEnd = v;
            QueueRedraw();
        }).AddTo(subs);
    }

    public void Bind(
        Entity celFolderEntity,
        ObservableSortedList<int, Entity> exposures,
        SelectionManager sm,
        CompositeDisposable subs)
    {
        _celFolderEntity = celFolderEntity;
        _selectionManager = sm;
        _exposures = exposures;
        _isSelected = sm.WorkingCelFolder.CurrentValue == _celFolderEntity;
        exposures.ObserveChanged().Subscribe(_ => QueueRedraw()).AddTo(subs);
        sm.WorkingCelFolder.Subscribe(workingCelFolder =>
        {
            bool isSelected = workingCelFolder == _celFolderEntity;
            if (isSelected == _isSelected) return;

            _isSelected = isSelected;
            QueueRedraw();
        }).AddTo(subs);
    }

    // ── Drawing ───────────────────────────────────────────────────────────────

    public override void _Draw()
    {
        if (_pixelsPerFrame <= 0f || _exposures == null) return;

        float h = Size.Y;
        float w = Size.X;
        float midY = h * 0.5f;
        float buttonW = CelButtonWidth;

        for (int i = 0; i < _exposures.Count; i++)
        {
            int frame = _exposures.GetKeyAtIndex(i);
            var exposureValue = _exposures.GetValueAtIndex(i);
            float x = FrameToX(frame);
            bool isBlankExposure = exposureValue.IsCelFolder;

            // ── Cel button
            var buttonRect = new Rect2(x, 0f, buttonW, h);
            if (buttonRect.End.X > 0f && buttonRect.Position.X < w)
            {
                Color buttonColor;
                if (_isCelButtonDragging && frame == _celButtonDragSourceFrame)
                    buttonColor = CelButtonNormalColor with { A = 0.35f }; // ghost while dragging
                else if (frame == _pressedFrame)
                    buttonColor = CelButtonPressedColor;
                else if (frame == _hoveredFrame)
                    buttonColor = CelButtonHoverColor;
                else
                    buttonColor = CelButtonNormalColor;

                DrawCelButton(buttonRect, buttonColor, isBlankExposure);
            }

            // ── Layer name label (draw for any visible frame) ─────────────────
            if (!isBlankExposure)
            {
                string name = exposureValue.Get<CommonLayerSetting>().Name.Value;
                float labelX = x + buttonW + LabelPad;
                float labelEnd = i + 1 < _exposures.Count
                    ? FrameToX(_exposures.GetKeyAtIndex(i + 1)) - SpanArrowHeadLength - LabelPad
                    : w;
                float maxW = labelEnd - labelX;
                if (maxW > 0f && labelX < w)
                    DrawString(LabelFont, new Vector2(labelX, midY + LabelFontSize * 0.35f),
                        name, HorizontalAlignment.Left, maxW, LabelFontSize, LabelColor);
            }

            // ── Exposure span (the dragged one is drawn as a preview instead) ─────
            if (TryGetExposureSpan(i, out var span) &&
                span.SourceFrame != DraggedSpanSourceFrame)
                DrawSpanArrow(span.ShaftStartX, span.TipX, midY,
                    GetExposureSpanColor(exposureValue));
        }

        // ── Right-click indicator line ────────────────────────────────────────
        if (_rightClickIndicatorFrame.HasValue)
        {
            float ix = FrameToX(_rightClickIndicatorFrame.Value);
            if (ix >= 0f && ix <= w)
                DrawLine(new Vector2(ix, 0f), new Vector2(ix, h),
                    new Color(1f, 1f, 1f, 0.75f), width: 1f);
        }

        // ── Cel button drag preview ───────────────────────────────────────────
        if (_isCelButtonDragging &&
            _celButtonDragTargetFrame.HasValue &&
            _celButtonDragTargetFrame != _celButtonDragSourceFrame)
        {
            int targetFrame = _celButtonDragTargetFrame.Value;
            bool isValid = !_exposures.ContainsKey(targetFrame);
            Color previewColor = isValid
                ? CelButtonHoverColor with { A = 0.85f }
                : new Color(0.9f, 0.25f, 0.25f, 0.6f);
            var previewRect = new Rect2(FrameToX(targetFrame), 0f, buttonW, h);
            DrawCelButton(previewRect, previewColor,
                _exposures[_celButtonDragSourceFrame.Value].IsCelFolder);
            DrawRect(previewRect, isValid ? PlayheadAccentColor : Colors.Red, filled: false, width: 1f);
        }

        // ── Exposure span drag preview: thicker, in the Playhead's accent color ───
        if (_isSpanDragging &&
            _pressedSpan is { } draggedArrow &&
            _spanDragTargetFrame.HasValue)
        {
            // Recomputed rather than reused: scroll/zoom may have changed since the press.
            float shaftStartX = draggedArrow.SourceFrame < _playbackStart
                ? FrameToX(_playbackStart)
                : FrameToX(draggedArrow.SourceFrame) + buttonW;
            float tipX = Mathf.Min(FrameToX(_spanDragTargetFrame.Value), w + SpanArrowHeadLength);
            DrawSpanArrow(shaftStartX, tipX, midY, PlayheadAccentColor, DraggedSpanArrowThickness);
        }

        if (_isSelected)
        {
            float width = 2.0f;
            DrawLine(new Vector2(0f, width), new Vector2(w, width), PlayheadAccentColor, width: width);
            DrawLine(new Vector2(0f, h - width / 2), new Vector2(w, h - width / 2), PlayheadAccentColor, width: width);
        }
    }

    /// <summary>Draws a cel button: a hollow outline for an Blank exposure, a solid bar otherwise.</summary>
    private void DrawCelButton(Rect2 rect, Color color, bool isBlankExposure)
    {
        if (isBlankExposure)
            DrawRect(rect.Grow(-BlankExposureStrokeWidth / 2f), color,
                filled: false, width: BlankExposureStrokeWidth);
        else
            DrawRect(rect, color);
    }

    /// <summary>Draws a horizontal exposure span. <paramref name="thickness"/> scales both shaft and head.</summary>
    private void DrawSpanArrow(float shaftStartX, float tipX, float midY, Color color, float thickness = 1f)
    {
        float headLength = SpanArrowHeadLength * thickness;
        if (tipX - shaftStartX <= headLength) return;

        DrawLine(new(shaftStartX, midY), new(tipX - headLength, midY), color, thickness);

        float headHalfWidth = SpanArrowHeadHalfWidth * thickness;
        Vector2 tip = new(tipX, midY);
        Vector2 p1 = new(tipX - headLength, midY - headHalfWidth);
        Vector2 p2 = new(tipX - headLength, midY + headHalfWidth);
        DrawColoredPolygon([tip, p1, p2], color);
    }

    private Color GetExposureSpanColor(Entity cel) =>
        cel.Get<CommonLayerSetting>().MarkColor.Value ?? ExposureSpanColor;

    // ── Coordinate helper ────────────────────────────────────────────────────

    /// <summary>Converts a pixel X position (local) to the nearest integer frame index.</summary>
    private int PositionToFrame(float posX) =>
        TimelineFrameGeometry.XToFrameRounded(posX, _pixelsPerFrame, _scrollOffset);

    /// <summary>Converts a pixel X position (local) to the frame index by flooring (used for right-click target).</summary>
    private int PositionToFrameFloor(float posX) =>
        TimelineFrameGeometry.XToFrameFloor(posX, _pixelsPerFrame, _scrollOffset);

    private float FrameToX(int frame) =>
        TimelineFrameGeometry.FrameToX(frame, _pixelsPerFrame, _scrollOffset);

    private bool IsInPlaybackRange(int frame) =>
        TimelineFrameGeometry.IsInPlaybackRange(frame, _playbackStart, _playbackEnd);

    // ── Input ────────────────────────────────────────────────────────────────

    /// <summary>Returns the frame key whose cel button contains <paramref name="posX"/>, or null.</summary>
    private int? CelButtonFrameAt(float posX)
    {
        if (_exposures == null) return null;

        int? frame = TimelineFrameGeometry.XToFrameInLeadingSpan(
            posX, _pixelsPerFrame, _scrollOffset, CelButtonWidth);
        return frame.HasValue && _exposures.ContainsKey(frame.Value) ? frame : null;
    }

    /// <summary>
    /// One exposure span (the CONTEXT.md term for the interval an exposure covers), resolved to both
    /// frames and pixels. <c>NextFrame</c> is the following exposure key (null when there is none);
    /// it equals <c>TipFrame</c> only when the span actually ends on that exposure rather than
    /// running out to <see cref="_playbackEnd"/>.
    /// <para>
    /// X-sheet tools call this a <i>hold</i> (Toon Boom Harmony: <c>Hold Exposure</c> /
    /// <c>Extend Exposure</c>). This type carries the view geometry too (<c>ShaftStartX</c>,
    /// <c>TipX</c>), so it is the drawable resolution of the span, not the domain concept alone.
    /// </para>
    /// </summary>
    private readonly record struct ExposureSpan(
        int SourceFrame,
        int TipFrame,
        int? NextFrame,
        float ShaftStartX,
        float TipX)
    {
        /// <summary>True when the arrow ends on the next exposure instead of at the playback end.</summary>
        public bool EndsOnNextExposure => NextFrame == TipFrame;
    }

    /// <summary>
    /// Resolves the exposure span leaving the exposure at <paramref name="index"/>. Returns false when
    /// that exposure draws no arrow (Blank exposure, out of playback range, or too short to render).
    /// Single source of truth for drawing, hit-testing and drag clamping.
    /// </summary>
    private bool TryGetExposureSpan(int index, out ExposureSpan span)
    {
        span = default;
        int source = _exposures.GetKeyAtIndex(index);
        if (source >= _playbackEnd) return false;
        if (_exposures.GetValueAtIndex(index).IsCelFolder) return false;

        int? next = index + 1 < _exposures.Count ? _exposures.GetKeyAtIndex(index + 1) : null;
        int tip;
        float shaftStartX;
        if (source < _playbackStart)
        {
            // Exposure straddles playbackStart: draw only the in-range remainder.
            if (!next.HasValue || !IsInPlaybackRange(next.Value)) return false;
            tip = next.Value;
            shaftStartX = FrameToX(_playbackStart);
        }
        else
        {
            tip = next.HasValue && next < _playbackEnd ? next.Value : _playbackEnd;
            shaftStartX = FrameToX(source) + CelButtonWidth;
        }

        float tipX = Mathf.Min(FrameToX(tip), Size.X + SpanArrowHeadLength);
        if (tipX - shaftStartX <= SpanArrowHeadLength) return false;

        span = new ExposureSpan(source, tip, next, shaftStartX, tipX);
        return true;
    }

    /// <summary>
    /// Returns the exposure span whose head (a cel-button-wide grab zone behind the tip) contains posX.
    /// </summary>
    private bool TryGetSpanArrowHeadAt(float posX, out ExposureSpan span)
    {
        span = default;
        if (_pixelsPerFrame <= 0f || _exposures == null) return false;

        float grabWidth = CelButtonWidth;
        for (int i = 0; i < _exposures.Count; i++)
        {
            if (!TryGetExposureSpan(i, out var candidate)) continue;

            float tipX = candidate.TipX;
            if (tipX < 0f || tipX > Size.X) continue;
            if (posX < tipX - grabWidth || posX >= tipX) continue;

            span = candidate;
            return true;
        }

        return false;
    }

    /// <summary>Source frame of the exposure span currently being dragged, or null.</summary>
    private int? DraggedSpanSourceFrame =>
        _isSpanDragging ? _pressedSpan?.SourceFrame : null;

    /// <summary>Clamps a drag target so the arrow keeps at least one frame and never passes the next exposure.</summary>
    private int ClampSpanDragTarget(ExposureSpan span, float posX)
    {
        int target = Mathf.Max(span.SourceFrame + 1, PositionToFrame(posX));
        // Retiming shifts later exposures, so only a next-ending arrow may grow past them.
        if (span.NextFrame.HasValue && !span.EndsOnNextExposure)
            target = Mathf.Min(target, span.NextFrame.Value);
        return target;
    }

    private void CommitSpanDrag()
    {
        if (!_isSpanDragging ||
            !_pressedSpan.HasValue ||
            !_spanDragTargetFrame.HasValue)
            return;

        var span = _pressedSpan.Value;
        int tip = span.TipFrame;
        int target = _spanDragTargetFrame.Value;

        if (span.EndsOnNextExposure)
        {
            // Retime: grow by inserting frames at the tip, shrink by deleting back to the target.
            int delta = target - tip;
            if (delta == 0) return;

            new CommandBuilder(ChangeExposureDurationCommandName)
                .SetObservableCollection(_exposures, exposures =>
                {
                    if (delta > 0)
                        TimelineFrameRetiming.InsertFrames(exposures, tip, delta);
                    else
                        TimelineFrameRetiming.DeleteFrames(exposures, target, -delta);
                })
                .Commit();
        }
        else if (!_exposures.ContainsKey(target))
        {
            // Arrow ran to playbackEnd: cap the exposure with an Blank exposure at the target.
            new CommandBuilder(ChangeExposureDurationCommandName)
                .SetObservableCollection(_exposures,
                    exposures => exposures.Add(target, _celFolderEntity))
                .Commit();
        }
    }

    private void ResetSpanDrag()
    {
        _pressedSpan = null;
        _isSpanDragging = false;
        _spanDragTargetFrame = null;
    }

    public override void _Input(InputEvent @event)
    {
        if (!_pressedSpan.HasValue || !AppHotkeys.UiCancel.IsPressedBy(@event)) return;

        ResetSpanDrag();
        QueueRedraw();
        GetViewport().SetInputAsHandled();
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseMotion motion)
        {
            int? newHovered = CelButtonFrameAt(motion.Position.X);
            if (newHovered != _hoveredFrame)
            {
                _hoveredFrame = newHovered;
                QueueRedraw();
            }

            // Exposure span drag: activate once threshold is exceeded, then track target frame
            if (_pressedSpan.HasValue)
            {
                if (!_isSpanDragging && Mathf.Abs(motion.Position.X - _pressStartX) > DragThreshold)
                    _isSpanDragging = true;

                if (_isSpanDragging)
                {
                    int newTarget = ClampSpanDragTarget(_pressedSpan.Value, motion.Position.X);
                    if (newTarget != _spanDragTargetFrame)
                    {
                        _spanDragTargetFrame = newTarget;
                        QueueRedraw();
                    }
                }
                AcceptEvent();
            }
            // Cel button drag: activate once threshold is exceeded, then track target frame
            else if (_pressedFrame.HasValue)
            {
                if (!_isCelButtonDragging && Mathf.Abs(motion.Position.X - _pressStartX) > DragThreshold)
                {
                    _isCelButtonDragging = true;
                    _celButtonDragSourceFrame = _pressedFrame;
                }
                if (_isCelButtonDragging)
                {
                    int newTarget = PositionToFrame(motion.Position.X);
                    if (newTarget != _celButtonDragTargetFrame)
                    {
                        _celButtonDragTargetFrame = newTarget;
                        QueueRedraw();
                    }
                }
            }
        }
        else if (@event is InputEventMouseButton btn && btn.ButtonIndex == MouseButton.Right && btn.Pressed)
        {
            int frame = PositionToFrameFloor(btn.Position.X);
            _rightClickIndicatorFrame = frame;
            QueueRedraw();
            RightClickMenu.PopupHide += OnMenuClosed;
            RightClickMenu.Popup(_celFolderEntity, frame);
            AcceptEvent();
        }
        else if (@event is InputEventMouseButton lbtn && lbtn.ButtonIndex == MouseButton.Left)
        {
            if (lbtn.Pressed)
            {
                if (TryGetSpanArrowHeadAt(lbtn.Position.X, out var span))
                {
                    _pressedSpan = span;
                    _pressStartX = lbtn.Position.X;
                    _isSpanDragging = false;
                    _spanDragTargetFrame = span.TipFrame;
                    AcceptEvent();
                    return;
                }

                int? celButtonFrame = CelButtonFrameAt(lbtn.Position.X);
                if (celButtonFrame.HasValue)
                {
                    _pressedFrame = celButtonFrame;
                    _pressStartX = lbtn.Position.X;
                    _isCelButtonDragging = false;
                    _celButtonDragSourceFrame = null;
                    _celButtonDragTargetFrame = null;
                    QueueRedraw();
                }
            }
            else // released
            {
                if (_pressedSpan.HasValue)
                {
                    CommitSpanDrag();
                    ResetSpanDrag();
                    QueueRedraw();
                    AcceptEvent();
                    return;
                }

                if (_isCelButtonDragging)
                {
                    // Commit the move if the target is valid (not occupied by another key)
                    if (_celButtonDragTargetFrame.HasValue
                        && _celButtonDragSourceFrame.HasValue
                        && _celButtonDragTargetFrame != _celButtonDragSourceFrame
                        && !_exposures.ContainsKey(_celButtonDragTargetFrame.Value))
                    {
                        int sourceFrame = _celButtonDragSourceFrame.Value;
                        int targetFrame = _celButtonDragTargetFrame.Value;
                        new CommandBuilder("Move Cel Exposure")
                            .SetObservableCollection(_exposures,
                                exposures =>
                                {
                                    var value = exposures[sourceFrame];
                                    exposures.Remove(sourceFrame);
                                    exposures.Add(targetFrame, value);
                                })
                            .Commit();
                    }
                    _isCelButtonDragging = false;
                    _celButtonDragSourceFrame = null;
                    _celButtonDragTargetFrame = null;
                }
                else if (_pressedFrame.HasValue)
                {
                    // Click (no drag): select this cel button's cel, then move the playhead to this frame.
                    int pressedFrame = _pressedFrame.Value;
                    if (_selectionManager != null && _exposures != null && _exposures.ContainsKey(pressedFrame))
                    {
                        var clickedCel = _exposures[pressedFrame];
                        int oldFrame = CurrentFrame.Value;
                        var cmd = new CommandBuilder("Select Cel Exposure", _celFolderEntity)
                            .SetProperty(CurrentFrame, oldFrame, pressedFrame);
                        var newLayers = SelectionManager.ResolveLayersForCelSelection(_celFolderEntity, clickedCel);
                        if (_selectionManager.NeedsTimelineSelectionCommit(newLayers))
                            cmd.SelectLayers(layers: newLayers);
                        cmd.CommitToLatest();
                    }
                }

                if (_pressedFrame.HasValue)
                {
                    _pressedFrame = null;
                    QueueRedraw();
                }
            }
        }
    }

    private void OnMenuClosed()
    {
        RightClickMenu.PopupHide -= OnMenuClosed;
        _rightClickIndicatorFrame = null;
        QueueRedraw();
    }

    public override int _GetCursorShape(Vector2 atPosition)
    {
        if (_pressedSpan.HasValue || TryGetSpanArrowHeadAt(atPosition.X, out _))
            return (int)CursorShape.Hsize;
        return CelButtonFrameAt(atPosition.X).HasValue
            ? (int)CursorShape.PointingHand
            : (int)CursorShape.Arrow;
    }
}
