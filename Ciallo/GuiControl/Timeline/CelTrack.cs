using System.Collections.Generic;
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
///   <item>For every exposure key a cell bar is drawn; consecutive bars are linked by
///         a line + arrowhead.</item>
/// </list>
/// Call <see cref="Observe"/> and <see cref="Bind"/> once after adding to the scene.
/// </summary>
[Tool]
public partial class CelTrack : Control
{
    // ── Tunable ──────────────────────────────────────────────────────────────
    public float BarWidthRatio = 0.5f; // bar width = ppf * ratio
    public float MaxBarWidth = 24f;
    public float BarWidth => Mathf.Min(_ppf * BarWidthRatio, MaxBarWidth);

    public float ArrowHeadLength = 7f;
    public float ArrowHeadHalfWidth = 4f;
    public float LabelPad = 3f;

    // ── State ─────────────────────────────────────────────────────────────────
    private float _ppf;
    private float _scrollOffset;
    private int _playbackStart;
    private int _playbackEnd;
    private ObservableSortedList<int, Entity> _exposures;
    private bool _isSelected;

    // ── Interaction state ─────────────────────────────────────────────────────
    private const float DragThreshold = 3f;
    private const float HollowRectStrokeWidth = 6f;
    private int? _hoveredFrame;
    private int? _pressedFrame;
    private float _dragStartX;
    private bool _isDragging;
    private int? _dragSourceFrame;
    private int? _dragTargetFrame;

    // ── Right-click indicator ─────────────────────────────────────────────────
    private int? _rightClickIndicatorFrame;

    // ── Entity references (set by Bind) ───────────────────────────────────────
    private Entity _celFolderEntity;
    private SelectionManager _selectionManager;
    public ReactiveProperty<int> CurrentFrame => _selectionManager.CurrentFrame;

    // ── Right-click menu ──────────────────────────────────────────────────────
    public CelTrackRightClickMenu RightClickMenu { get; set; }

    // ── Theme ─────────────────────────────────────────────────────────────────
    public Color BarNormalColor;
    public Color BarHoverColor;
    public Color BarPressedColor;
    public Color LabelColor;
    public Color ArrowColor;
    public Font LabelFont;
    public int LabelFontSize;
    public Color HintSelectedColor = new(207 / 255f, 167 / 255f, 106 / 255f, 1f); // hardcoded orange idencial to palyhead color.

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
        BarNormalColor = normalStyleBox.BgColor;
        var hoverStyleBox = (StyleBoxFlat)GetThemeStylebox("hover", "Button");
        BarHoverColor = hoverStyleBox.BgColor;
        var pressedStyleBox = (StyleBoxFlat)GetThemeStylebox("pressed", "Button");
        BarPressedColor = pressedStyleBox.BgColor;
        LabelColor = GetThemeColor("font_color", "Button");
        ArrowColor = LabelColor with { A = 0.8f };
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
                _ppf = t.ppf;
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
        if (_ppf <= 0f || _exposures == null) return;

        float h = Size.Y;
        float w = Size.X;
        float midY = h * 0.5f;
        float barW = BarWidth;

        var frames = new List<int>();
        foreach (var kv in _exposures)
            frames.Add(kv.Key);

        float playbackStartX = FrameToX(_playbackStart);
        float playbackEndX = FrameToX(_playbackEnd);

        // Arrow from playbackStart to first in-range frame.
        // If the exposure started before playbackStart, use that cel's mark color.
        int? firstInRange = null;
        int? previousFrame = null;
        foreach (int frame in frames)
        {
            if (frame < _playbackStart)
            {
                previousFrame = frame;
                continue;
            }
            if (frame < _playbackEnd)
                firstInRange = frame;
            break;
        }

        if (previousFrame.HasValue && firstInRange.HasValue)
        {
            var previousExposure = _exposures[previousFrame.Value];
            if (!previousExposure.IsCelFolder)
            {
                float tipX = Mathf.Min(FrameToX(firstInRange.Value), w + ArrowHeadLength);
                DrawArrow(playbackStartX, tipX, midY, GetCelArrowColor(previousExposure));
            }
        }

        for (int i = 0; i < frames.Count; i++)
        {
            int frame = frames[i];
            float x = FrameToX(frame);
            var layerE = _exposures[frame];
            bool isEmpty = layerE.IsCelFolder;

            // ── Cel drag bar
            var barRect = new Rect2(x, 0f, barW, h);
            if (barRect.End.X > 0f && barRect.Position.X < w)
            {
                Color barColor;
                if (_isDragging && frame == _dragSourceFrame)
                    barColor = BarNormalColor with { A = 0.35f }; // ghost while dragging
                else if (frame == _pressedFrame)
                    barColor = BarPressedColor;
                else if (frame == _hoveredFrame)
                    barColor = BarHoverColor;
                else
                    barColor = BarNormalColor;

                if (isEmpty)
                    DrawEmptyCelButton(barRect, barColor);
                else
                    DrawRect(barRect, barColor);
            }

            // ── Layer name label (draw for any visible frame) ─────────────────
            if (!isEmpty)
            {
                string name = layerE.Get<CommonLayerSetting>().Name.Value;
                float labelX = x + barW + LabelPad;
                int? nextAny = i + 1 < frames.Count ? frames[i + 1] : null;
                float labelEnd = nextAny.HasValue
                    ? FrameToX(nextAny.Value) - ArrowHeadLength - LabelPad
                    : w;
                float maxW = labelEnd - labelX;
                if (maxW > 0f && labelX < w)
                    DrawString(LabelFont, new Vector2(labelX, midY + LabelFontSize * 0.35f),
                        name, HorizontalAlignment.Left, maxW, LabelFontSize, LabelColor);
            }

            if (frame < _playbackStart || frame >= _playbackEnd) continue; // skip arrows for out-of-range frames
            if (isEmpty) continue;

            // Next frame within playback range (or none)
            int? nextInRange = null;
            for (int j = i + 1; j < frames.Count; j++)
            {
                if (frames[j] >= _playbackStart && frames[j] < _playbackEnd) { nextInRange = frames[j]; break; }
            }

            // Arrow to next bar, or to playbackEnd for the last in-range frame.
            float tipX = nextInRange.HasValue
                ? Mathf.Min(FrameToX(nextInRange.Value), w + ArrowHeadLength)
                : Mathf.Min(playbackEndX, w + ArrowHeadLength);
            DrawArrow(x + barW, tipX, midY, GetCelArrowColor(layerE));
        }

        // ── Right-click indicator line ────────────────────────────────────────
        if (_rightClickIndicatorFrame.HasValue)
        {
            float ix = FrameToX(_rightClickIndicatorFrame.Value);
            if (ix >= 0f && ix <= w)
                DrawLine(new Vector2(ix, 0f), new Vector2(ix, h),
                    new Color(1f, 1f, 1f, 0.75f), width: 1f);
        }

        // ── Drag preview ──────────────────────────────────────────────────────
        if (_isDragging && _dragTargetFrame.HasValue && _dragTargetFrame != _dragSourceFrame)
        {
            int targetFrame = _dragTargetFrame.Value;
            float targetX = FrameToX(targetFrame);
            bool isValid = !_exposures.ContainsKey(targetFrame);
            Color previewColor = isValid
                ? BarHoverColor with { A = 0.85f }
                : new Color(0.9f, 0.25f, 0.25f, 0.6f);
            var previewRect = new Rect2(targetX, 0f, barW, h);
            if (_exposures[_dragSourceFrame.Value].IsCelFolder)
                DrawEmptyCelButton(previewRect, previewColor);
            else
                DrawRect(previewRect, previewColor);
            DrawRect(previewRect, isValid ? LabelColor : Colors.Red, filled: false, width: 1f);
        }

        if (_isSelected)
        {
            float width = 2.0f;
            DrawLine(new Vector2(0f, width), new Vector2(w, width), HintSelectedColor, width: width);
            DrawLine(new Vector2(0f, h - width / 2), new Vector2(w, h - width / 2), HintSelectedColor, width: width);
        }
    }

    private void DrawEmptyCelButton(Rect2 rect, Color color)
    {
        float inset = HollowRectStrokeWidth / 2f;
        DrawRect(rect.Grow(-inset), color, filled: false, width: HollowRectStrokeWidth);
    }

    private void DrawArrow(float shaftStart, float tipX, float midY, Color color)
    {
        if (tipX - shaftStart <= ArrowHeadLength) return;

        DrawLine(new(shaftStart, midY), new(tipX - ArrowHeadLength, midY), color, 1.0f);

        Vector2 tip = new(tipX, midY);
        Vector2 p1 = new(tipX - ArrowHeadLength, midY - ArrowHeadHalfWidth);
        Vector2 p2 = new(tipX - ArrowHeadLength, midY + ArrowHeadHalfWidth);
        DrawColoredPolygon([tip, p1, p2], color);
    }

    private Color GetCelArrowColor(Entity layerE) =>
        layerE.Get<CommonLayerSetting>().MarkColor.Value ?? ArrowColor;

    // ── Coordinate helper ────────────────────────────────────────────────────

    /// <summary>Converts a pixel X position (local) to the nearest integer frame index.</summary>
    private int PositionToFrame(float posX) =>
        TimelineFrameGeometry.XToFrameRounded(posX, _ppf, _scrollOffset);

    /// <summary>Converts a pixel X position (local) to the frame index by flooring (used for right-click target).</summary>
    private int PositionToFrameFloor(float posX) =>
        TimelineFrameGeometry.XToFrameFloor(posX, _ppf, _scrollOffset);

    private float FrameToX(int frame) =>
        TimelineFrameGeometry.FrameToX(frame, _ppf, _scrollOffset);

    // ── Input ────────────────────────────────────────────────────────────────

    /// <summary>Returns the frame key whose bar contains <paramref name="posX"/>, or null.</summary>
    private int? FrameAt(float posX)
    {
        if (_ppf <= 0f || _exposures == null) return null;
        float barW = BarWidth;
        foreach (var kv in _exposures)
        {
            float x = FrameToX(kv.Key);
            if (posX >= x && posX < x + barW)
                return kv.Key;
        }
        return null;
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseMotion motion)
        {
            int? newHovered = FrameAt(motion.Position.X);
            if (newHovered != _hoveredFrame)
            {
                _hoveredFrame = newHovered;
                QueueRedraw();
            }

            // Drag: activate once threshold is exceeded, then track target frame
            if (_pressedFrame.HasValue)
            {
                if (!_isDragging && Mathf.Abs(motion.Position.X - _dragStartX) > DragThreshold)
                {
                    _isDragging = true;
                    _dragSourceFrame = _pressedFrame;
                }
                if (_isDragging)
                {
                    int newTarget = PositionToFrame(motion.Position.X);
                    if (newTarget != _dragTargetFrame)
                    {
                        _dragTargetFrame = newTarget;
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
                int? f = FrameAt(lbtn.Position.X);
                if (f.HasValue)
                {
                    _pressedFrame = f;
                    _dragStartX = lbtn.Position.X;
                    _isDragging = false;
                    _dragSourceFrame = null;
                    _dragTargetFrame = null;
                    QueueRedraw();
                }
            }
            else // released
            {
                if (_isDragging)
                {
                    // Commit the move if the target is valid (not occupied by another key)
                    if (_dragTargetFrame.HasValue
                        && _dragSourceFrame.HasValue
                        && _dragTargetFrame != _dragSourceFrame
                        && !_exposures.ContainsKey(_dragTargetFrame.Value))
                    {
                        int src = _dragSourceFrame.Value;
                        int tgt = _dragTargetFrame.Value;
                        new CommandBuilder("Move Cel Exposure")
                            .SetObservableCollection(_exposures,
                                exposures =>
                                {
                                    var value = exposures[src];
                                    exposures.Remove(src);
                                    exposures.Add(tgt, value);
                                })
                            .Commit();
                    }
                    _isDragging = false;
                    _dragSourceFrame = null;
                    _dragTargetFrame = null;
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
                        var newWorkingLayer = _selectionManager.ComputeWorkingLayerForCelButtonSelection(_celFolderEntity, clickedCel);
                        if (!newWorkingLayer.IsNull)
                            cmd.SetTarget(newWorkingLayer).SetWorkingLayer();
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

    public override int _GetCursorShape(Vector2 atPosition) =>
        FrameAt(atPosition.X).HasValue ? (int)CursorShape.PointingHand : (int)CursorShape.Arrow;
}
