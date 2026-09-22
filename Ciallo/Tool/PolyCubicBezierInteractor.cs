using System;
using System.Collections.Generic;
using Ciallo.Geometry;
using Ciallo.Rendering;
using Godot;

namespace Ciallo.Tool;

/// <summary>Shared pen interaction; concrete tools supply the preview and sampled output.</summary>
public abstract class PolyCubicBezierInteractor : CapturingInteraction
{
    public static readonly Trigger Closed = new("PolyCubicBezierClosed");

    private readonly List<Anchor> _anchors = [];
    private readonly Stack<Anchor> _redo = [];
    private readonly List<Vector2> _points = [];
    private readonly List<int> _segmentPointCounts = [];
    private readonly List<Vector2> _firstSegmentPoints = [];
    private readonly CubicBezierSampler _sampler = new();
    private int _committedPointCount;
    private Vector2 _sampledFirstOutHandle;
    private readonly List<Vector2> _dotPositions = [];
    private readonly Vector2[] _handlePoints = new Vector2[2];
    private static readonly float[] HandleValues = [1f, 1f];
    private bool _dotsDirty;
    private bool _handlesDirty;
    private Anchor _pending;
    private bool _dragging;
    private bool _placingFirst;
    private bool _closing;
    private bool _closureDragged;
    private Vector2 _closurePressScreenPosition;
    private bool _showClosureHandles;
    private bool _closed;
    private Node2D _wireframe;
    private readonly List<StrokeView> _handleLines = [];
    private MultiMeshInstance2D _controlPoints;

    private const float SampleSpacing = 8f;
    private const float MaxDeviation = 2f;
    private const float CloseDistancePixels = 8f;
    private const float ClosureDragDistancePixels = 3f;

    private readonly record struct Anchor(
        Vector2 Position, Vector2 InHandle, Vector2 OutHandle, PaintStrokeSnapTarget? SnapTarget)
    {
        public Anchor WithHandle(Vector2 handle) => this with
        {
            InHandle = Position * 2 - handle,
            OutHandle = handle,
        };
    }

    protected virtual bool CloseOnConfirm => false;
    protected PaintStrokeSnapTarget? StartSnapTarget => _anchors[0].SnapTarget;
    protected PaintStrokeSnapTarget? EndSnapTarget => _anchors[^1].SnapTarget;
    protected PaintStrokeSnapTarget? PreviewSnapTarget { get; private set; }
    protected virtual PaintStrokeSnapTarget? FindSnapTarget(Vector2 position) => null;
    protected abstract void CreatePreview();
    protected abstract void UpdatePreview(IReadOnlyList<Vector2> points);
    protected abstract void Commit(PolylineSamples samples, bool closed);
    protected abstract void ClearPreview();

    public override TimeSpan MovingMinInterval => TimeSpan.Zero;

    public override void Start(CursorButtonData data)
    {
        Input.MouseMode = Input.MouseModeEnum.Hidden;
        var snap = FindSnapTarget(data.WorldPosition);
        Vector2 start = snap?.HitPoint ?? data.WorldPosition;
        _pending = new(start, start, start, snap);
        _anchors.Add(_pending);
        _points.Add(start);
        _sampledFirstOutHandle = start;
        _committedPointCount = 1;
        _dotsDirty = _handlesDirty = true;
        _placingFirst = true;
        _dragging = true;
        _closed = false;
        CreatePreview();
        BuildWireframe();
        RefreshPreview(data.WorldPosition);
    }

    public override void Moving(CursorMotionData data)
    {
        if (_dragging)
        {
            if (_closing)
                UpdateClosureHandle(data);
            else
                _pending = _pending.WithHandle(data.WorldPosition);
            _handlesDirty = true;
        }
        RefreshPreview(data.WorldPosition);
    }

    public override bool OnMouseButton(InputEventMouseButton button, CursorButtonData data)
    {
        if (button.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown &&
            Input.IsKeyPressed(Key.Alt) && !_dragging)
        {
            float factor = button.ButtonIndex == MouseButton.WheelUp ? 1.15f : 1f / 1.15f;
            var anchor = _anchors[^1];
            _anchors[^1] = anchor with
            {
                OutHandle = anchor.Position + (anchor.OutHandle - anchor.Position) * factor,
            };
            _handlesDirty = true;
            RefreshPreview(data.WorldPosition);
            return true;
        }

        if (button.ButtonIndex != MouseButton.Left)
            return base.OnMouseButton(button, data);

        if (button.Pressed)
        {
            if (!_dragging)
            {
                if (IsOverStart(data.WorldPosition))
                {
                    _closing = true;
                    _dragging = true;
                    _closureDragged = false;
                    _closurePressScreenPosition = data.ScreenPosition;
                    _pending = ClosureAnchor;
                    _handlesDirty = true;
                    RefreshPreview(data.WorldPosition);
                    return true;
                }

                _redo.Clear();
                var snap = FindSnapTarget(data.WorldPosition);
                Vector2 position = snap?.HitPoint ?? data.WorldPosition;
                _pending = new(position, position, position, snap);
                _dragging = true;
                _dotsDirty = _handlesDirty = true;
                RefreshPreview(data.WorldPosition);
            }
            return true;
        }

        if (_dragging)
        {
            if (_closing)
            {
                UpdateClosureHandle(data);
                _closed = true;
                Fire(Closed);
                return true;
            }
            _pending = _pending.WithHandle(data.WorldPosition);
            if (_placingFirst)
            {
                _anchors[0] = _pending;
                _sampledFirstOutHandle = _pending.OutHandle;
            }
            else
                AddAnchor(_pending);
            _placingFirst = false;
            _dragging = false;
            _dotsDirty = _handlesDirty = true;
            RefreshPreview(data.WorldPosition);
        }
        return true;
    }

    public override bool OnKey(InputEventKey key, CursorButtonData data)
    {
        if (AppHotkeys.Global.EditUndo.IsPressedBy(key) && !_dragging)
        {
            if (_anchors.Count == 1)
            {
                Fire(InteractionManager.CancelRequested);
                return true;
            }
            _redo.Push(_anchors[^1]);
            _anchors.RemoveAt(_anchors.Count - 1);
            _committedPointCount -= _segmentPointCounts[^1];
            _segmentPointCounts.RemoveAt(_segmentPointCounts.Count - 1);
            _dotsDirty = _handlesDirty = true;
            RefreshPreview(data.WorldPosition);
            return true;
        }
        if (AppHotkeys.Global.EditRedo.IsPressedBy(key) && !_dragging && _redo.Count > 0)
        {
            AddAnchor(_redo.Pop());
            _dotsDirty = _handlesDirty = true;
            RefreshPreview(data.WorldPosition);
            return true;
        }
        return base.OnKey(key, data);
    }

    public override void End(CursorButtonData data)
    {
        bool closed = CloseOnConfirm || _closed;
        if (_anchors.Count > 1)
        {
            TrimPendingPoints();
            UpdateFirstSegment(_closed ? _pending : _anchors[0]);
            if (_closed)
                AppendCubic(_points, _anchors[^1], _pending);
            else if (CloseOnConfirm)
                AppendCubic(_points, _anchors[^1] with { OutHandle = _anchors[^1].Position }, ClosureAnchor);
            Commit(PolylineSamples.Uniform(_points), closed);
        }
        Clear();
    }

    public override void Cancel() => Clear();

    private Anchor ClosureAnchor => _anchors[0] with
    {
        InHandle = _anchors[0].Position,
        SnapTarget = null,
    };

    private void UpdateClosureHandle(CursorButtonData data)
    {
        _closureDragged |= data.ScreenPosition.DistanceSquaredTo(_closurePressScreenPosition) >=
            ClosureDragDistancePixels * ClosureDragDistancePixels;
        Vector2 outgoing = _pending.OutHandle;
        Vector2 direction = data.WorldPosition - _pending.Position;
        if (_closureDragged && direction != Vector2.Zero)
        {
            float length = _anchors[0].Position.DistanceTo(_anchors[0].OutHandle);
            outgoing = _pending.Position + direction.Normalized() * length;
        }
        // At zero incoming length its direction is undefined; keep the last outgoing direction.
        _pending = _pending with
        {
            InHandle = _closureDragged ? _pending.Position * 2 - data.WorldPosition : _pending.Position,
            OutHandle = outgoing,
        };
    }

    private void UpdateFirstSegment(Anchor first)
    {
        if (_sampledFirstOutHandle == first.OutHandle) return;
        _firstSegmentPoints.Clear();
        AppendCubic(_firstSegmentPoints, first, _anchors[1]);
        int oldCount = _segmentPointCounts[0];
        int newCount = _firstSegmentPoints.Count;
        if (newCount == oldCount)
        {
            for (int i = 0; i < newCount; i++)
                _points[i + 1] = _firstSegmentPoints[i];
        }
        else
        {
            _points.RemoveRange(1, oldCount);
            _points.InsertRange(1, _firstSegmentPoints);
            _segmentPointCounts[0] = newCount;
            _committedPointCount += newCount - oldCount;
        }
        _sampledFirstOutHandle = first.OutHandle;
    }

    private bool IsOverStart(Vector2 cursor)
    {
        if (_anchors.Count < 2) return false;
        var transform = _wireframe.GetGlobalTransformWithCanvas();
        return (transform * cursor).DistanceSquaredTo(transform * _anchors[0].Position) <=
            CloseDistancePixels * CloseDistancePixels;
    }

    private void RefreshPreview(Vector2 cursor)
    {
        bool closing = _closing || (!_dragging && IsOverStart(cursor));
        if (_showClosureHandles != closing)
        {
            _showClosureHandles = closing;
            _handlesDirty = true;
        }
        PreviewSnapTarget = _dragging ? _pending.SnapTarget : closing ? null : FindSnapTarget(cursor);
        TrimPendingPoints();
        if (_closing)
            UpdateFirstSegment(_pending);
        if (closing)
            AppendCubic(_points, _anchors[^1], _closing ? _pending : ClosureAnchor);
        else if (!_placingFirst)
        {
            cursor = PreviewSnapTarget?.HitPoint ?? cursor;
            var next = _dragging ? _pending : new Anchor(cursor, cursor, cursor, null);
            if (next.Position.DistanceSquaredTo(_anchors[^1].Position) > 1e-6f)
                AppendCubic(_points, _anchors[^1], next);
        }
        UpdatePreview(_points);
        UpdateWireframe();
    }

    private void TrimPendingPoints() =>
        _points.RemoveRange(_committedPointCount, _points.Count - _committedPointCount);

    private void AddAnchor(Anchor anchor)
    {
        TrimPendingPoints();
        AppendCubic(_points, _anchors[^1], anchor);
        _segmentPointCounts.Add(_points.Count - _committedPointCount);
        if (_anchors.Count == 1)
            _sampledFirstOutHandle = _anchors[0].OutHandle;
        _anchors.Add(anchor);
        _committedPointCount = _points.Count;
    }

    private void AppendCubic(List<Vector2> result, Anchor from, Anchor to) =>
        _sampler.AppendTo(result, from.Position, from.OutHandle, to.InHandle, to.Position,
            SampleSpacing, MaxDeviation);

    private void BuildWireframe()
    {
        _wireframe = new Node2D();
        Document.Get<WorldOverlay>().AddChild(_wireframe);
        for (int i = 0; i < 3; i++)
        {
            var line = new StrokeView { Material = AutoloadRendering.WireframeMaterial };
            _wireframe.AddChild(line);
            _handleLines.Add(line);
        }
        _controlPoints = AutoloadRendering.CreateDots();
        _wireframe.AddChild(_controlPoints);
    }

    private void UpdateWireframe()
    {
        if (_dotsDirty)
        {
            _dotPositions.Clear();
            foreach (var anchor in _anchors)
                _dotPositions.Add(anchor.Position);
            if (_dragging && !_placingFirst)
                _dotPositions.Add(_pending.Position);
            _controlPoints.SetDotGeometry(_dotPositions, 4f);
            _dotsDirty = false;
        }
        if (_handlesDirty)
        {
            if (_showClosureHandles)
            {
                var end = _closing ? _pending : ClosureAnchor;
                SetHandleLine(_handleLines[0], end.InHandle, end.Position);
                SetHandleLine(_handleLines[1], _anchors[^1].Position, _anchors[^1].OutHandle);
                SetHandleLine(_handleLines[2], end.Position, end.OutHandle);
            }
            else
            {
                var current = _dragging ? _pending : _anchors[^1];
                SetHandleLine(_handleLines[0], current.InHandle, current.Position);
                SetHandleLine(_handleLines[1], current.Position, current.OutHandle);
                SetHandleLine(_handleLines[2], current.Position, current.Position);
            }
            _handlesDirty = false;
        }
    }

    private void SetHandleLine(StrokeView line, Vector2 from, Vector2 to)
    {
        if (from.DistanceSquaredTo(to) > 1e-6f)
        {
            _handlePoints[0] = from;
            _handlePoints[1] = to;
            line.SetGeometry(_handlePoints, HandleValues, HandleValues);
        }
        else
            line.SetGeometry(Array.Empty<Vector2>(), Array.Empty<float>(), Array.Empty<float>());
    }

    private void Clear()
    {
        ClearPreview();
        _wireframe.QueueFree();
        _wireframe = null;
        _controlPoints = null;
        _handleLines.Clear();
        _anchors.Clear();
        _redo.Clear();
        _points.Clear();
        _segmentPointCounts.Clear();
        _firstSegmentPoints.Clear();
        _committedPointCount = 0;
        _dotPositions.Clear();
        _pending = default;
        PreviewSnapTarget = null;
        _dragging = false;
        _placingFirst = false;
        _closing = false;
        _closureDragged = false;
        _showClosureHandles = false;
        _closed = false;
        Input.MouseMode = Input.MouseModeEnum.Visible;
    }
}
