using System.Collections.Generic;
using System.Linq;
using Ciallo.Data;
using Ciallo.Geometry;
using Ciallo.Rendering;
using Frent;
using Godot;
using R3;

namespace Ciallo.Tool;

[RegisterState]
public class PolylineBezierDeformHover : PolylineNoSelectionHover
{
    public BezierPoint[] Curve;

    private Node2D _wireframe;
    public Body CenterlineBody;
    public readonly List<Body[]> PointBodies = [];

    protected List<Entity> SelectedShapes = [];

    public bool CanDeform
    {
        get
        {
            if (Curve == null) return false;
            bool centerlineHovered = CenterlineBody.IsHovered;
            bool pointHovered = PointBodies.SelectMany(bs => bs).Any(b => b?.IsHovered == true);
            return centerlineHovered || pointHovered;
        }
    }

    public override void BeforeSourceExit(Interaction src)
    {
        if (src is PolylineBezierDeformInteractor interactor)
            Curve = interactor.Curve;
    }

    public override void Start(CursorButtonData data)
    {
        Subs = new();
        var worldBody = Document.Get<WorldBody>();
        SelectedShapes.AddRange(Document.Get<SelectionManager>().SelectedShapes);

        // Enable cursor detections
        worldBody.EnableHoverDetection = true;
        worldBody.CursorWorldPosition = data.WorldPosition;

        Document.Get<WorldBody>().HoveringBody.Subscribe(ToggleWireframe).AddTo(Subs);
        SelectedShapes.ForEach(e => e.Get<Body>().ProcessMode = Node.ProcessModeEnum.Disabled);

        // Data
        if (Curve == null)
        {
            int pointCount = SelectedShapes.Select(e => e.Get<SampledPolyline>().Count).Sum();
            if (pointCount <= 1) return;
            if (SelectedShapes.Count == 1)
            {
                // Straight fit
                Curve = SelectedShapes.Single().Get<SampledPolyline>().Positions.Value.FitBezier(2);
            }
            else
            {
                // Cluster polylines first, then fit
                var polylines = SelectedShapes
                    .Select(e => (IReadOnlyList<Vector2>)e.Get<SampledPolyline>().Positions.Value)
                    .ToList();
                var fitPoints = Geometry.Geometry.ClusterPolylines(polylines);
                Curve = fitPoints.FitBezier(2);
            }
        }

        // Curve wireframe view
        _wireframe = new();
        _wireframe.AddChild(DrawBezierCenterline(Curve));
        foreach (var handleNode in DrawBezierHandle(Curve))
        {
            _wireframe.AddChild(handleNode);
        }
        _wireframe.AddChild(DrawBezierControlPoint(Curve));
        Document.Get<WorldOverlay>().AddChild(_wireframe);

        // Bodies
        CenterlineBody = worldBody.CreateAddStrokeCenterline(Curve.TessellateWithCache().polyline, AppPreference.StrokeWireframeRadius * 10);
        CenterlineBody.MouseDefaultCursorShape = Control.CursorShape.PointingHand;

        Vector2 bodySize = AppPreference.StrokeDotRadius * 2 * Vector2.One;
        for (int i = 0; i < Curve.Length; i++)
        {
            var pointBodies = new Body[3];
            PointBodies.Add(pointBodies);
            var pt = Curve[i];
            var anchorBody = worldBody.CreateAddRectBody(bodySize, pt.P, CursorRectFlags.ScreenSize);
            anchorBody.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
            pointBodies[0] = anchorBody;
            if (i > 0 && !pt.In.IsZeroApprox())
            {
                var inBody = worldBody.CreateAddRectBody(bodySize, pt.PIn, CursorRectFlags.ScreenSize);
                inBody.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
                pointBodies[1] = inBody;
            }
            if (i < Curve.Length - 1 && !pt.Out.IsZeroApprox())
            {
                var outBody = worldBody.CreateAddRectBody(bodySize, pt.POut, CursorRectFlags.ScreenSize);
                outBody.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
                pointBodies[2] = outBody;
            }
        }
    }

    public static StrokeView DrawBezierCenterline(IReadOnlyList<BezierPoint> curve, StrokeView update = null)
    {
        update ??= new StrokeView()
        {
            Material = AutoloadRendering.WireframeMaterial,
        };
        var (polyline, _) = curve.Tessellate(64);
        update.SetGeometry(polyline, AppPreference.StrokeWireframeRadius);
        return update;
    }

    public static StrokeView[] DrawBezierHandle(IReadOnlyList<BezierPoint> curve, StrokeView[] update = null)
    {
        StrokeView[] result;
        if (update == null)
        {
            result = new StrokeView[curve.Count];
            for (int i = 0; i < curve.Count; i++)
                result[i] = new StrokeView { Material = AutoloadRendering.WireframeMaterial };
        }
        else
        {
            result = update;
        }

        for (int i = 0; i < curve.Count; i++)
        {
            var pt = curve[i];
            List<Vector2> pts = [];
            if (i > 0 && !pt.In.IsZeroApprox())
                pts.Add(pt.PIn);
            pts.Add(pt.P);
            if (i < curve.Count - 1 && !pt.Out.IsZeroApprox())
                pts.Add(pt.POut);
            result[i].SetGeometry(pts, AppPreference.StrokeWireframeRadius);
        }
        return result;
    }

    public static MultiMeshInstance2D DrawBezierControlPoint(IReadOnlyList<BezierPoint> curve, MultiMeshInstance2D update = null)
    {
        update ??= AutoloadRendering.CreateDots();
        var positions = new List<Vector2>();
        for (int i = 0; i < curve.Count; i++)
        {
            var pt = curve[i];
            positions.Add(pt.P);
            if (i > 0 && !pt.In.IsZeroApprox())
                positions.Add(pt.PIn);
            if (i < curve.Count - 1 && !pt.Out.IsZeroApprox())
                positions.Add(pt.POut);
        }
        update.SetDotGeometry(positions, AppPreference.StrokeDotRadius);
        return update;
    }

    public override void Cancel()
    {
        Curve = null;
        _wireframe?.QueueFree();
        _wireframe = null;
        foreach (var b in PointBodies.SelectMany(b => b)) b?.QueueFree();
        PointBodies.Clear();
        CenterlineBody?.QueueFree();
        CenterlineBody = null;
        SelectedShapes.ForEach(e => e.Get<Body>().ProcessMode = Node.ProcessModeEnum.Inherit);
        SelectedShapes.Clear();
        base.Cancel();
    }
}