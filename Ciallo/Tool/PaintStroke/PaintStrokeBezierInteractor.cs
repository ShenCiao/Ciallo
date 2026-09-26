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

/// <summary>
/// Interactive rational quadratic Bézier curve drawing mode for paint strokes.
/// Click to place anchor, drag to adjust weights via tangent/normal decomposition.
/// </summary>
/// <remarks>Uses a degree-elevated rational cubic to support phase 3 weight editing.</remarks>
[RegisterState]
public class PaintStrokeBezierInteractor : CapturingInteraction
{
    [StateAccess]
    public PaintStrokeTool Tool { get; set; }

    public static Trigger QuadBezierEnd = new(nameof(QuadBezierEnd));

    public Entity BrushE;
    public StrokeView StrokePreview;
    private readonly PaintStrokeGeometryBuilder _geometryBuilder = new();
    private Func<float, float> _radiusSampler;

    private PaintStrokeSnapTarget? _startSnapTarget;
    private PaintStrokeSnapTarget? _endSnapTarget;
    private readonly List<Vector2> _snapHintPoints = new(2);
    private MultiMeshInstance2D _snapDots;

    // Bezier state
    private Vector2 _p0;
    private Vector2 _p1;
    private Vector2 _p2;
    private float _w1 = 1f;
    private float _w2 = 1f;
    private float _phase3BaseW1 = 1f;
    private float _phase3BaseW2 = 1f;
    private float _phase3ControlPointConvergence;

    // Interaction phases
    private enum Phase
    {
        DraggingP2,      // Phase 1: dragging P2 endpoint
        PlacingP1,       // Phase 2: moving mouse to preview P1, click to confirm
        AdjustingWeights // Phase 3: dragging to adjust weights
    }
    private Phase _phase = Phase.DraggingP2;
    private Vector2 _startScreenPosition;
    private bool _canConfirmP2OnRelease;
    private const float EndpointDragThresholdPixels = 4f;

    // Wireframe visualization
    private Node2D _wireframe;
    private StrokeView _wireframeHandleLine;
    private StrokeView _wireframeDragHint; // Phase 3: drag hint line from p1 to cursor
    private MultiMeshInstance2D _wireframeControlPoints;

    // Phase 3 interaction tuning. Values are in world-units per log-weight.
    private const float NormalWeightSensitivity = 0.012f;
    private const float TangentWeightSensitivity = 0.012f;
    private const float NormalControlPointConvergence = 0.018f;
    // Numerical guards only; there is no business-imposed weight range.
    private const float MinimumPositiveWeight = 1e-6f;
    private const float MaximumSafeLogWeight = 80f;
    private const int TessellationSubdivisions = 32;

    public override TimeSpan MovingMinInterval => TimeSpan.Zero;

    public override void Start(CursorButtonData data)
    {
        Input.MouseMode = Input.MouseModeEnum.Hidden;

        // Selection in brush library has higher priority
        if (AppStrokeBrushLibrary.HasSelection)
        {
            var setting = AppStrokeBrushLibrary.SelectedBrushSetting.CurrentValue;
            new CommandBuilder("Use Library Stroke Brush", Document.World.Create())
                .NewStrokeBrush(setting).SetWorkingStrokeBrush().Commit();
            AppStrokeBrushLibrary.SelectedIndex.Value = -1;
        }
        BrushE = Document.Get<SelectionManager>().WorkingStrokeBrush.Value;
        _radiusSampler = BrushE.Get<StrokeBrushSetting>().ToRadiusSampler();

        var brushMaterial = BrushE.Get<StrokeBrushMaterial>();
        StrokePreview = new StrokeView { Material = brushMaterial };
        var layerView = PrimaryLayer.Get<ShapeLayerView>();
        layerView.AddChild(StrokePreview);

        _snapDots = AutoloadRendering.CreateDots();
        Document.Get<WorldOverlay>().AddChild(_snapDots);

        // Phase 1: Initialize P0, start dragging P2
        _p0 = data.WorldPosition;
        _p2 = data.WorldPosition;
        _p1 = data.WorldPosition;
        _w1 = 1f;
        _w2 = 1f;
        _phase3ControlPointConvergence = 0f;
        _phase = Phase.DraggingP2;
        _startScreenPosition = data.ScreenPosition;
        _canConfirmP2OnRelease = false;

        _startSnapTarget = Tool.TryFindSnapTarget(data.WorldPosition);
        _endSnapTarget = null;

        BuildWireframe();
        RefreshPressurePreview();
        UpdateSnapHint();
    }

    public override void Moving(CursorMotionData data)
    {
        switch (_phase)
        {
            case Phase.DraggingP2:
                _canConfirmP2OnRelease |= HasReachedEndpointDragThreshold(data.ScreenPosition);
                UpdateP2Preview(data.WorldPosition);
                break;

            case Phase.PlacingP1:
                UpdateP1Preview(data.WorldPosition);
                break;

            case Phase.AdjustingWeights:
                UpdateWeightPreview(data.WorldPosition);
                break;
        }

        RefreshVisuals();
    }

    public override void End(CursorButtonData data)
    {
        // This is only called when exiting the LeftBezier state entirely
        CommitStroke(data);
        Clear();
    }

    public override void Cancel() => Clear();

    public override bool OnMouseButton(InputEventMouseButton button, CursorButtonData data)
    {
        if (button.ButtonIndex == MouseButton.Left && button.Pressed && _phase == Phase.PlacingP1)
        {
            BeginWeightAdjustment(data.WorldPosition);
            RefreshVisuals();
            return true;
        }

        if (button.ButtonIndex == MouseButton.Left && !button.Pressed)
        {
            if (_phase == Phase.DraggingP2)
            {
                // Include the release position in case no motion event delivered it.
                _canConfirmP2OnRelease |= HasReachedEndpointDragThreshold(data.ScreenPosition);
                if (_canConfirmP2OnRelease)
                {
                    ConfirmP2(data.WorldPosition);
                }
                else
                {
                    // A first click within the threshold keeps P2 following the cursor.
                    // The next release confirms even a segment shorter than the threshold.
                    _canConfirmP2OnRelease = true;
                    UpdateP2Preview(data.WorldPosition);
                }

                RefreshVisuals();
                return true;
            }

            if (_phase == Phase.AdjustingWeights)
            {
                Fire(QuadBezierEnd);
                return true;
            }
        }

        return base.OnMouseButton(button, data);
    }

    private bool HasReachedEndpointDragThreshold(Vector2 screenPosition) =>
        screenPosition.DistanceSquaredTo(_startScreenPosition) >=
        EndpointDragThresholdPixels * EndpointDragThresholdPixels;

    private void UpdateP2Preview(Vector2 cursor)
    {
        _p2 = cursor;
        _p1 = (_p0 + _p2) * 0.5f;
        RefreshEndSnapTarget(cursor);
    }

    private void UpdateP1Preview(Vector2 cursor) => _p1 = cursor;

    private void UpdateWeightPreview(Vector2 cursor)
    {
        AdjustWeightsFromDrag(cursor);
        UpdateDragHint(cursor);
    }

    private void ConfirmP2(Vector2 cursor)
    {
        _p2 = cursor;
        _p1 = (_p0 + _p2) * 0.5f;
        RefreshEndSnapTarget(cursor);
        _phase = Phase.PlacingP1;
    }

    private void BeginWeightAdjustment(Vector2 cursor)
    {
        _p1 = cursor;
        _phase3BaseW1 = _w1;
        _phase3BaseW2 = _w2;
        _phase3ControlPointConvergence = 0f;
        _phase = Phase.AdjustingWeights;
    }

    private void RefreshVisuals()
    {
        UpdateWireframe();
        RefreshPressurePreview();
        UpdateSnapHint();
    }

    /// <summary>
    /// Adjust the elevated cubic's internal weights from drag components.
    /// The normal follows P1's internal angle bisector, pointing away from the anchors.
    /// Its positive direction increases bulge.
    /// The orthogonal tangent is oriented to agree with P0-to-P2.
    /// </summary>
    private void AdjustWeightsFromDrag(Vector2 dragWorldPos)
    {
        // A coincident point pair cannot define the P1 angle and baseline.
        if (_p0.DistanceSquaredTo(_p2) < 1e-4f)
            return;

        Vector2 startToControl = _p1 - _p0;
        Vector2 endToControl = _p1 - _p2;
        if (startToControl.LengthSquared() < 1e-4f || endToControl.LengthSquared() < 1e-4f)
            return;

        Vector2 bisector = startToControl.Normalized() + endToControl.Normalized();
        // Opposing rays (a straight segment through P1) have no outward direction.
        if (bisector.LengthSquared() < 1e-10f)
            return;

        Vector2 normalDir = bisector.Normalized();
        Vector2 tangentDir = new Vector2(-normalDir.Y, normalDir.X);
        if (tangentDir.Dot(_p2 - _p0) < 0f)
            tangentDir = -tangentDir;

        // Decompose drag
        Vector2 dragDelta = dragWorldPos - _p1;
        float normalComponent = dragDelta.Dot(normalDir);
        float tangentComponent = dragDelta.Dot(tangentDir);

        // The product controls overall bulge; the ratio controls which elevated
        // handle is followed more closely.
        float normalLog = normalComponent * NormalWeightSensitivity;
        float tangentLog = tangentComponent * TangentWeightSensitivity;
        _phase3ControlPointConvergence = 1f - Mathf.Exp(
            -Mathf.Max(0f, normalComponent) * NormalControlPointConvergence);
        float baseLogMean = 0.5f * (Mathf.Log(_phase3BaseW1) + Mathf.Log(_phase3BaseW2));
        float baseLogRatio = 0.5f * (Mathf.Log(_phase3BaseW1) - Mathf.Log(_phase3BaseW2));
        // C1 is the P0-P1 handle and C2 is the P1-P2 handle. Therefore a
        // positive tangent component (toward P2) must favor C2, i.e. reduce
        // w1/w2; the negative direction (toward P0) favors C1.
        float logRatio = baseLogRatio - tangentLog;
        _w1 = SafePositiveExp(baseLogMean + normalLog + logRatio);
        _w2 = SafePositiveExp(baseLogMean + normalLog - logRatio);
    }

    private static float SafePositiveExp(float logWeight)
    {
        return Mathf.Max(MinimumPositiveWeight,
            Mathf.Exp(Mathf.Clamp(logWeight, -MaximumSafeLogWeight, MaximumSafeLogWeight)));
    }

    private void CommitStroke(CursorButtonData data)
    {
        var targetLayer = Tool.ResolveStrokeTargetLayer();
        if (targetLayer.IsNull || targetLayer.IsDyingOrDead)
            return;

        // P2 is confirmed at the end of Phase 1. The Phase 3 release position only
        // controls the weights and must never become the stroke's endpoint snap.
        RefreshEndSnapTarget(_p2);

        var samples = PaintStrokeSnap.BuildRepairedGeometry(
            Tool.Arrangement.ArrReady.CurrentValue,
            PolylineSamples.Uniform(TessellateCurve(), Tool.CurvePressure.Value),
            _startSnapTarget,
            _endSnapTarget,
            Tool.SnapDistance.Value);

        var geometry = _geometryBuilder.Build(samples, Tool.PressureTaper, _radiusSampler);

        new CommandBuilder("Paint Stroke (Bezier)", PrimaryLayer.World.Create())
            .NewStroke()
            .AddToLayerTree(targetLayer)
            .SetProperty(e => e.Get<StrokeSetting>().Brush, BrushE)
            .SetSampledPolyline(
                geometry.Positions.ToImmutableArray(),
                geometry.Radii.ToImmutableArray(),
                geometry.Pressures.ToImmutableArray(),
                geometry.Tilts.ToImmutableArray())
            .Commit();
    }

    internal void RefreshPressurePreview()
    {
        var geometry = _geometryBuilder.Build(
            PolylineSamples.Uniform(TessellateCurve(), Tool.CurvePressure.Value), Tool.PressureTaper, _radiusSampler);
        StrokePreview.SetGeometry(geometry.Positions, geometry.Radii, geometry.Pressures);
    }

    private Vector2[] TessellateCurve()
    {
        // Degree elevation preserves the original quadratic exactly when both
        // internal weights are one.
        Vector2 elevatedC1 = _p0.Lerp(_p1, 2f / 3f);
        Vector2 elevatedC2 = _p1.Lerp(_p2, 1f / 3f);
        Vector2 c1 = elevatedC1.Lerp(_p1, _phase3ControlPointConvergence);
        Vector2 c2 = elevatedC2.Lerp(_p1, _phase3ControlPointConvergence);
        return RationalCubicBezier.Tessellate(
            _p0, c1, c2, _p2, 1f, _w1, _w2, 1f, TessellationSubdivisions);
    }

    private void BuildWireframe()
    {
        _wireframe = new Node2D();
        Document.Get<WorldOverlay>().AddChild(_wireframe);

        // Handle line (p0 -> p1 -> p2)
        _wireframeHandleLine = new StrokeView
        {
            Material = AutoloadRendering.WireframeMaterial,
            Visible = false
        };
        _wireframe.AddChild(_wireframeHandleLine);

        // Drag hint line (p1 -> cursor in Phase 3)
        _wireframeDragHint = new StrokeView
        {
            Material = AutoloadRendering.WireframeMaterial
        };
        _wireframe.AddChild(_wireframeDragHint);

        // Control points
        _wireframeControlPoints = AutoloadRendering.CreateDots();
        _wireframe.AddChild(_wireframeControlPoints);
    }

    private void UpdateWireframe()
    {
        if (_wireframe == null) return;

        // Update handle line (p0 -> p1 -> p2)
        _wireframeHandleLine.Visible = _phase != Phase.DraggingP2;
        Vector2[] handlePoints = [_p0, _p1, _p2];
        float[] handleRadii = [1f, 1f, 1f];
        float[] handlePressures = [1f, 1f, 1f];
        _wireframeHandleLine.SetGeometry(handlePoints, handleRadii, handlePressures);

        // Update control points
        _wireframeControlPoints.SetDotGeometry([_p0, _p1, _p2], 4f);

        // Drag hint is only visible in Phase 3, updated separately in UpdateDragHint
    }

    private void UpdateDragHint(Vector2 cursorPos)
    {
        if (_wireframeDragHint == null) return;

        // Draw line from p1 to cursor
        Vector2[] dragHintPoints = [_p1, cursorPos];
        float[] dragHintRadii = [1f, 1f];
        float[] dragHintPressures = [1f, 1f];
        _wireframeDragHint.SetGeometry(dragHintPoints, dragHintRadii, dragHintPressures);
    }

    internal void RefreshSnapping()
    {
        RefreshEndSnapTarget(_p2);
        UpdateSnapHint();
    }

    private void RefreshEndSnapTarget(Vector2 worldPosition)
    {
        _endSnapTarget = Tool.TryFindSnapTarget(worldPosition);
    }

    private void UpdateSnapHint()
    {
        _snapHintPoints.Clear();
        if (_startSnapTarget is { } startTarget)
            _snapHintPoints.Add(startTarget.HitPoint);
        if (_endSnapTarget is { } endTarget)
            _snapHintPoints.Add(endTarget.HitPoint);

        _snapDots.Visible = _snapHintPoints.Count > 0;
        if (_snapHintPoints.Count > 0)
            _snapDots.SetDotGeometry(_snapHintPoints, AppPreference.StrokeDotRadius);
    }

    public void Clear()
    {
        StrokePreview?.QueueFree();
        StrokePreview = null;
        _startSnapTarget = null;
        _endSnapTarget = null;
        _snapDots?.QueueFree();
        _snapDots = null;
        _wireframe?.QueueFree();
        _wireframe = null;
        _wireframeHandleLine = null;
        _wireframeDragHint = null;
        _wireframeControlPoints = null;
        Input.MouseMode = Input.MouseModeEnum.Visible;
        _phase = Phase.DraggingP2; // Reset to initial phase
    }
}
