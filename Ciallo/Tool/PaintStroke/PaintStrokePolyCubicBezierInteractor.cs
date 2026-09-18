using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Ciallo.Command;
using Ciallo.Data;
using Ciallo.Geometry;
using Ciallo.Rendering;
using Frent;
using Godot;

namespace Ciallo.Tool;

/// <summary>Continuous cubic Bézier pen interaction. Segments are stored as sampled polylines.</summary>
[RegisterState]
public class PaintStrokePolyCubicBezierInteractor : CapturingInteraction
{
    [StateAccess] public PaintStrokeTool Tool { get; set; }
    public Entity BrushE;
    public StrokeView StrokePreview;
    private readonly PaintStrokeGeometryBuilder _geometryBuilder = new();
    private Func<float, float> _radiusSampler;
    private PolylineSamples _previewSamples;

    private readonly List<Vector2> _anchors = [];
    private readonly List<Vector2> _outHandles = [];
    private readonly List<Vector2> _inHandles = [];
    private readonly Stack<AnchorSnapshot> _redo = [];
    private Vector2 _pendingEnd;
    private Vector2 _pendingOutHandle;
    private bool _dragging;
    private PaintStrokeSnapTarget? _startSnapTarget;
    private readonly List<PaintStrokeSnapTarget?> _anchorSnapTargets = [];
    private PaintStrokeSnapTarget? _pendingSnapTarget;
    private PaintStrokeSnapTarget? _hoverSnapTarget;
    private readonly List<Vector2> _snapHintPoints = new(2);
    private MultiMeshInstance2D _snapDots;

    private Node2D _wireframe;
    private readonly List<StrokeView> _handleLines = [];
    private MultiMeshInstance2D _controlPoints;

    private const int SegmentSubdivisions = 24;

    private readonly record struct AnchorSnapshot(
        Vector2 Anchor,
        Vector2 InHandle,
        Vector2 OutHandle,
        PaintStrokeSnapTarget? SnapTarget);

    public override TimeSpan MovingMinInterval => TimeSpan.Zero;

    public override void Start(CursorButtonData data)
    {
        Input.MouseMode = Input.MouseModeEnum.Hidden;
        BrushE = Document.Get<SelectionManager>().WorkingStrokeBrush.Value;
        _radiusSampler = BrushE.Get<StrokeBrushSetting>().ToRadiusSampler();
        StrokePreview = new StrokeView { Material = BrushE.Get<StrokeBrushMaterial>() };
        PrimaryLayer.Get<ShapeLayerView>().AddChild(StrokePreview);
        _snapDots = AutoloadRendering.CreateDots();
        Document.Get<WorldOverlay>().AddChild(_snapDots);
        _startSnapTarget = Tool.TryFindSnapTarget(data.WorldPosition);
        Vector2 start = _startSnapTarget?.HitPoint ?? data.WorldPosition;
        _anchors.Clear();
        _outHandles.Clear();
        _inHandles.Clear();
        _anchorSnapTargets.Clear();
        _redo.Clear();
        _anchors.Add(start);
        _outHandles.Add(start);
        _inHandles.Add(start);
        _pendingEnd = start;
        _pendingOutHandle = start;
        _anchorSnapTargets.Add(null);
        _pendingSnapTarget = null;
        _hoverSnapTarget = _startSnapTarget;
        _dragging = true;
        BuildWireframe();
        RefreshPreview(data.WorldPosition);
    }

    public override void Moving(CursorMotionData data)
    {
        if (_dragging)
        {
            _pendingOutHandle = data.WorldPosition;
            RefreshPreview(data.WorldPosition);
            return;
        }
        _hoverSnapTarget = Tool.TryFindSnapTarget(data.WorldPosition);
        RefreshPreview(_hoverSnapTarget?.HitPoint ?? data.WorldPosition);
    }

    public override bool OnMouseButton(InputEventMouseButton button, CursorButtonData data)
    {
        if (button.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown && Input.IsKeyPressed(Key.Alt) && !_dragging)
        {
            float factor = button.ButtonIndex == MouseButton.WheelUp ? 1.15f : 1f / 1.15f;
            Vector2 anchor = _anchors[^1];
            Vector2 handleVector = _outHandles[^1] - anchor;
            if (handleVector.LengthSquared() > 1e-6f)
            {
                float length = _outHandles[^1].DistanceTo(anchor) * factor;
                _outHandles[^1] = anchor + handleVector.Normalized() * length;
                RefreshPreview(data.WorldPosition);
            }
            return true;
        }

        if (button.ButtonIndex != MouseButton.Left)
            return base.OnMouseButton(button, data);

        if (button.Pressed)
        {
            if (!_dragging)
            {
                _redo.Clear();
                // The press position fixes the next anchor (P2). Dragging
                // afterwards only places its outgoing handle (P3).
                _pendingSnapTarget = Tool.TryFindSnapTarget(data.WorldPosition);
                _pendingEnd = _pendingSnapTarget?.HitPoint ?? data.WorldPosition;
                _pendingOutHandle = _pendingEnd;
                _dragging = true;
                RefreshPreview(data.WorldPosition);
            }
            return true;
        }

        if (_dragging)
        {
            if (_anchors.Count == 1 && _anchors[^1] == _pendingEnd)
                _outHandles[^1] = data.WorldPosition;
            else
            {
                Vector2 endpoint = _pendingEnd;
                _anchors.Add(endpoint);
                _outHandles.Add(data.WorldPosition);
                _inHandles.Add(endpoint - (data.WorldPosition - endpoint));
                _anchorSnapTargets.Add(_pendingSnapTarget);
            }
            _dragging = false;
            _hoverSnapTarget = null;
            RefreshPreview(data.WorldPosition);
            return true;
        }
        return true;
    }

    public override bool OnKey(InputEventKey key, CursorButtonData data)
    {
        if (AppHotkeys.Global.EditUndo.IsPressedBy(key) && !_dragging)
        {
            if (_anchors.Count > 1)
            {
                int last = _anchors.Count - 1;
                _redo.Push(new AnchorSnapshot(
                    _anchors[last], _inHandles[last], _outHandles[last], _anchorSnapTargets[last]));
                _anchors.RemoveAt(_anchors.Count - 1);
                _outHandles.RemoveAt(_outHandles.Count - 1);
                _inHandles.RemoveAt(_inHandles.Count - 1);
                _anchorSnapTargets.RemoveAt(last);
                _pendingEnd = _anchors[^1];
                _pendingOutHandle = _outHandles[^1];
                RefreshPreview(data.WorldPosition);
            }
            return true;
        }
        if (AppHotkeys.Global.EditRedo.IsPressedBy(key) && !_dragging && _redo.Count > 0)
        {
            GD.Print($"Is undo pressed with redo: {AppHotkeys.Global.EditUndo.IsJustPressed}");
            var snapshot = _redo.Pop();
            _anchors.Add(snapshot.Anchor);
            _inHandles.Add(snapshot.InHandle);
            _outHandles.Add(snapshot.OutHandle);
            _anchorSnapTargets.Add(snapshot.SnapTarget);
            _pendingEnd = snapshot.Anchor;
            _pendingOutHandle = snapshot.OutHandle;
            RefreshPreview(data.WorldPosition);
            return true;
        }
        return base.OnKey(key, data);
    }

    public override void End(CursorButtonData data)
    {
        CommitStroke();
        Clear();
    }

    public override void Cancel() => Clear();

    private void RefreshPreview(Vector2 cursor)
    {
        _previewSamples = PolylineSamples.Uniform(BuildPolyline(cursor));
        RefreshPressureTaper();
        UpdateWireframe();
        UpdateSnapHint();
    }

    internal void RefreshPressureTaper()
    {
        var geometry = _geometryBuilder.Build(_previewSamples, Tool.PressureTaper, _radiusSampler);
        StrokePreview.SetGeometry(geometry.Positions, geometry.Radii, geometry.Pressures);
    }

    private List<Vector2> BuildPolyline(Vector2 cursor, bool includePending = true)
    {
        var result = new List<Vector2>();
        if (_anchors.Count == 0) return result;
        result.Add(_anchors[0]);
        for (int i = 0; i + 1 < _anchors.Count; i++)
        {
            Vector2 p0 = _anchors[i], p3 = _anchors[i + 1];
            Vector2 p1 = _outHandles[i];
            Vector2 p2 = _inHandles[i + 1];
            AppendCubic(result, p0, p1, p2, p3);
        }
        if (_anchors.Count > 0)
        {
            if (!includePending && _dragging)
                return result;
            Vector2 endpoint = _dragging ? _pendingEnd : cursor;
            if (endpoint.DistanceSquaredTo(_anchors[^1]) > 1e-6f)
            {
                Vector2 outgoing = _outHandles[^1];
                Vector2 incoming = _dragging
                    ? endpoint - (_pendingOutHandle - endpoint)
                    : endpoint;
                AppendCubic(result, _anchors[^1], outgoing, incoming, endpoint);
            }
        }
        return result;
    }

    private void BuildWireframe()
    {
        _wireframe = new Node2D();
        Document.Get<WorldOverlay>().AddChild(_wireframe);
        for (int i = 0; i < 2; i++)
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
        if (_wireframe == null) return;
        var dots = new List<Vector2>(_anchors);
        foreach (var line in _handleLines)
            line.SetGeometry(Array.Empty<Vector2>(), Array.Empty<float>(), Array.Empty<float>());
        Vector2 anchor;
        Vector2 incoming;
        Vector2 outgoing;
        if (_dragging)
        {
            anchor = _pendingEnd;
            incoming = anchor - (_pendingOutHandle - anchor);
            outgoing = _pendingOutHandle;
            dots.Add(_pendingEnd);
        }
        else
        {
            anchor = _anchors[^1];
            incoming = _inHandles[^1];
            outgoing = _outHandles[^1];
        }
        if (incoming.DistanceSquaredTo(anchor) > 1e-6f)
            SetHandleLine(_handleLines[0], incoming, anchor);
        if (outgoing.DistanceSquaredTo(anchor) > 1e-6f)
            SetHandleLine(_handleLines[1], anchor, outgoing);
        _controlPoints.SetDotGeometry(dots, 4f);
    }

    private static void SetHandleLine(StrokeView line, Vector2 from, Vector2 to)
    {
        line.SetGeometry([from, to], [1f, 1f], [1f, 1f]);
    }

    private static void AppendCubic(List<Vector2> result, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3)
    {
        for (int i = 1; i <= SegmentSubdivisions; i++)
            result.Add(p0.BezierInterpolate(p1, p2, p3, (float)i / SegmentSubdivisions));
    }

    private void CommitStroke()
    {
        var points = BuildPolyline(_pendingEnd, includePending: false);
        if (points.Count < 2) return;
        var targetLayer = Tool.ResolveStrokeTargetLayer();
        if (targetLayer.IsNull || targetLayer.IsDyingOrDead) return;
        var samples = PaintStrokeSnap.BuildRepairedGeometry(
            Tool.Arrangement.ArrReady.CurrentValue,
            PolylineSamples.Uniform(points),
            _startSnapTarget,
            _anchorSnapTargets.Count > 0 ? _anchorSnapTargets[^1] : null,
            AppPreference.PaintStrokeSnapDistance.Value);
        var geometry = _geometryBuilder.Build(samples, Tool.PressureTaper, _radiusSampler);
        new CommandBuilder("Paint Stroke (Poly Cubic Bézier)", PrimaryLayer.World.Create())
            .NewStroke().AddToLayerTree(targetLayer)
            .SetProperty(e => e.Get<StrokeSetting>().Brush, BrushE)
            .SetSampledPolyline(geometry.Positions.ToImmutableArray(), geometry.Radii.ToImmutableArray(),
                geometry.Pressures.ToImmutableArray(), geometry.Tilts.ToImmutableArray())
            .Commit();
    }

    private void UpdateSnapHint()
    {
        if (_snapDots == null) return;
        _snapHintPoints.Clear();
        if (_startSnapTarget is { } start)
            _snapHintPoints.Add(start.HitPoint);
        var candidate = _dragging ? _pendingSnapTarget : _hoverSnapTarget;
        if (candidate is { } end && (!startOrSame(end.HitPoint)))
            _snapHintPoints.Add(end.HitPoint);
        _snapDots.Visible = _snapHintPoints.Count > 0;
        if (_snapHintPoints.Count > 0)
            _snapDots.SetDotGeometry(_snapHintPoints, AppPreference.StrokeDotRadius);

        bool startOrSame(Vector2 point) => _startSnapTarget is { } s && s.HitPoint.IsEqualApprox(point);
    }

    public void Clear()
    {
        StrokePreview?.QueueFree(); StrokePreview = null;
        _wireframe?.QueueFree(); _wireframe = null;
        _snapDots?.QueueFree(); _snapDots = null;
        foreach (var line in _handleLines) line.QueueFree();
        _handleLines.Clear();
        _controlPoints = null;
        _anchors.Clear(); _outHandles.Clear(); _inHandles.Clear(); _anchorSnapTargets.Clear(); _dragging = false;
        _redo.Clear();
        _startSnapTarget = null; _pendingSnapTarget = null; _hoverSnapTarget = null;
        Input.MouseMode = Input.MouseModeEnum.Visible;
    }
}
