using System;
using System.Linq;
using Ciallo.Command;
using Ciallo.Data;
using Ciallo.Geometry;
using Ciallo.Rendering;
using Frent;
using Godot;

namespace Ciallo.Tool;

[RegisterState]
public class PolylineBezierDeformInteractor : CapturingInteraction
{
    public BezierPoint[] Curve;

    private float[][] _polyTs; // [polylineIdx][pointIdx] → bezier t on Curve, computed in Start

    private Entity[] _processingEs;
    private Vector2[][] _origPolylines; // [polylineIdx][pointIdx], snapshot at Start
    private Vector2[][] _currPolylines; // working buffer, updated every Moving tick
    private Vector2[][] _origCurveSamples;

    private enum DragMode { Centerline, Anchor, InHandle, OutHandle }

    private DragMode _dragMode;
    private int _dragPointIndex; // Curve control point index (non-centerline modes)

    private BezierPoint[] _origCurve; // snapshot of Curve at Start
    private Vector2 _dragStartWorldPos;

    // Centerline drag only
    private int _centerlineSegIdx; // segment index of the clicked point
    private float _centerlineSegT; // local t within that segment
    private Vector2 _origClickPos; // curve position at the clicked polyT

    // Wireframe live view
    private Node2D _wireframe;
    private StrokeView _wireframeCenterline;
    private StrokeView[] _wireframeHandles;
    private MultiMeshInstance2D _wireframeControlPoints;

    public override void BeforeSourceExit(Interaction src)
    {
        if (src is not PolylineBezierDeformHover hover)
            throw new Exception("Unexpected source state for PolylineBezierDeformInteractor.");

        Curve = hover.Curve;

        if (hover.CenterlineBody.IsHovered)
        {
            _dragMode = DragMode.Centerline;
            return;
        }

        for (int i = 0; i < hover.PointBodies.Count; i++)
        {
            var bodies = hover.PointBodies[i];
            for (int j = 0; j < 3; j++)
            {
                if (bodies[j]?.IsHovered != true) continue;
                _dragPointIndex = i;
                _dragMode = j switch
                {
                    0 => DragMode.Anchor,
                    1 => DragMode.InHandle,
                    _ => DragMode.OutHandle,
                };
                return;
            }
        }
    }

    public override void Start(CursorButtonData data)
    {
        _processingEs = Document.Get<SelectionManager>().SelectedShapes.ToArray();
        _origCurve = (BezierPoint[])Curve.Clone();
        _dragStartWorldPos = data.WorldPosition;

        // Snapshot positions and allocate working buffers
        _origPolylines = new Vector2[_processingEs.Length][];
        _currPolylines = new Vector2[_processingEs.Length][];
        for (int i = 0; i < _processingEs.Length; i++)
        {
            var positions = _processingEs[i].Get<SampledPolyline>().Positions.Value;
            _origPolylines[i] = positions.ToArray();
            _currPolylines[i] = new Vector2[positions.Length];
            positions.CopyTo(_currPolylines[i]);
        }

        // Compute bezier t for each polyline point and precompute original curve samples
        _polyTs = new float[_processingEs.Length][];
        _origCurveSamples = new Vector2[_processingEs.Length][];
        for (int i = 0; i < _origPolylines.Length; i++)
            (_origCurveSamples[i], _polyTs[i]) = _origCurve.GetClosestPoint(_origPolylines[i]);

        if (_dragMode == DragMode.Centerline)
        {
            _origCurve.GetClosestPoint(data.WorldPosition, out float polyT);
            (_centerlineSegIdx, _centerlineSegT) = polyT.Modf();
            _origClickPos = _origCurve.Sample(polyT);
        }

        // Build wireframe live view
        _wireframe = new Node2D();
        _wireframeCenterline = PolylineBezierDeformHover.DrawBezierCenterline(Curve);
        _wireframe.AddChild(_wireframeCenterline);
        _wireframeHandles = PolylineBezierDeformHover.DrawBezierHandle(Curve);
        foreach (var h in _wireframeHandles)
            _wireframe.AddChild(h);
        _wireframeControlPoints = PolylineBezierDeformHover.DrawBezierControlPoint(Curve);
        _wireframe.AddChild(_wireframeControlPoints);
        Document.Get<WorldOverlay>().AddChild(_wireframe);
    }

    public override void Moving(CursorMotionData data)
    {
        var totalDisplacement = data.WorldPosition - _dragStartWorldPos;
        bool altHeld = Input.IsKeyPressed(Key.Alt);

        // Update Curve — always derived from _origCurve to avoid per-frame drift
        switch (_dragMode)
        {
            case DragMode.Centerline:
                {
                    // Adobe Illustrator style: both handles shift so B(t) tracks the mouse.
                    // d = (mouse - origClickPos) / (3(1-t)t)
                    float denom = 3f * (1f - _centerlineSegT) * _centerlineSegT;
                    if (Mathf.IsZeroApprox(denom)) break;

                    var handleDelta = (data.WorldPosition - _origClickPos) / denom;
                    Curve[_centerlineSegIdx] = _origCurve[_centerlineSegIdx]
                        .WithOut(_origCurve[_centerlineSegIdx].Out + handleDelta);
                    Curve[_centerlineSegIdx + 1] = _origCurve[_centerlineSegIdx + 1]
                        .WithIn(_origCurve[_centerlineSegIdx + 1].In + handleDelta);
                    break;
                }
            case DragMode.Anchor:
                {
                    var origPt = _origCurve[_dragPointIndex];
                    Curve[_dragPointIndex] = origPt.WithPoint(origPt.P + totalDisplacement);
                    break;
                }
            case DragMode.InHandle:
                {
                    var origPt = _origCurve[_dragPointIndex];
                    var newIn = origPt.In + totalDisplacement;
                    if (altHeld || origPt.Out.IsZeroApprox())
                        Curve[_dragPointIndex] = origPt.WithIn(newIn);
                    else
                    {
                        float angleDelta = newIn.Angle() - origPt.In.Angle();
                        float lengthDelta = newIn.Length() - origPt.In.Length();
                        var newOut = origPt.Out.Normalized().Rotated(angleDelta) * (origPt.Out.Length() + lengthDelta);
                        Curve[_dragPointIndex] = new BezierPoint(origPt.P, newIn, newOut);
                    }
                    break;
                }
            case DragMode.OutHandle:
                {
                    var origPt = _origCurve[_dragPointIndex];
                    var newOut = origPt.Out + totalDisplacement;
                    if (altHeld || origPt.In.IsZeroApprox())
                        Curve[_dragPointIndex] = origPt.WithOut(newOut);
                    else
                    {
                        float angleDelta = newOut.Angle() - origPt.Out.Angle();
                        float lengthDelta = newOut.Length() - origPt.Out.Length();
                        var newIn = origPt.In.Normalized().Rotated(angleDelta) * (origPt.In.Length() + lengthDelta);
                        Curve[_dragPointIndex] = new BezierPoint(origPt.P, newIn, newOut);
                    }
                    break;
                }
        }

        // Update wireframe live view
        PolylineBezierDeformHover.DrawBezierCenterline(Curve, _wireframeCenterline);
        PolylineBezierDeformHover.DrawBezierHandle(Curve, _wireframeHandles);
        PolylineBezierDeformHover.DrawBezierControlPoint(Curve, _wireframeControlPoints);

        // Apply curve displacement to polyline views
        for (int i = 0; i < _processingEs.Length; i++)
        {
            var e = _processingEs[i];
            var geom = e.Get<SampledPolyline>();
            for (int j = 0; j < _currPolylines[i].Length; j++)
            {
                _currPolylines[i][j] = _origPolylines[i][j] + (Curve.Sample(_polyTs[i][j]) - _origCurveSamples[i][j]);
            }

            if (e.Has<StrokeSetting>())
                e.Get<StrokeView>().SetGeometry(_currPolylines[i], geom.Radii.Value, geom.Pressures.Value);
            if (e.Has<VectorFillMarkerSetting>())
                e.Get<VectorFillMarkerView>().SetGeometry(_currPolylines[i], geom.Radii.Value);
            if (e.Has<FilledPolygonSetting>())
                e.Get<Polygon2D>().SetPolygonFromRawRing(_currPolylines[i]);
        }
    }

    public override void End(CursorButtonData data)
    {
        bool changed = false;
        for (int i = 0; i < _currPolylines.Length && !changed; i++)
            for (int j = 0; j < _currPolylines[i].Length && !changed; j++)
                if (!_currPolylines[i][j].IsEqualApprox(_origPolylines[i][j]))
                    changed = true;

        if (changed)
        {
            var cmd = new CommandBuilder("Bezier Deform Shapes");
            foreach (var (i, e) in _processingEs.Index())
                cmd.SetTarget(e).SetSampledPolyline([.. _currPolylines[i]]);
            cmd.Commit();
        }

        Clear();
    }

    public override void Cancel()
    {
        foreach (var e in _processingEs)
        {
            var geom = e.Get<SampledPolyline>();
            var pts = geom.Positions.Value;
            if (e.Has<StrokeSetting>())
                e.Get<StrokeView>().SetGeometry(pts, geom.Radii.Value, geom.Pressures.Value);
            if (e.Has<VectorFillMarkerSetting>())
                e.Get<VectorFillMarkerView>().SetGeometry(pts, geom.Radii.Value);
            if (e.Has<FilledPolygonSetting>())
                e.Get<Polygon2D>().SetPolygonFromRawRing(pts);
        }
        Clear();
    }

    private void Clear()
    {
        _wireframe?.QueueFree();
        _wireframe = null;
        _wireframeCenterline = null;
        _wireframeHandles = null;
        _wireframeControlPoints = null;
        _processingEs = null;
        _origPolylines = null;
        _currPolylines = null;
        _origCurveSamples = null;
        _polyTs = null;
        _origCurve = null;
        _dragStartWorldPos = default;
        _dragPointIndex = 0;
        _centerlineSegIdx = 0;
        _centerlineSegT = 0f;
    }
}
