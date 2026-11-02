using System.Collections.Generic;
using System.Linq;
using Ciallo.Data;
using Ciallo.Rendering;
using Godot;

namespace Ciallo.Tool;

[RegisterState]
public class LiquifyHover : Interaction
{
    [StateAccess]
    public LiquifyTool Tool { get; set; }

    private StrokeView _brushCircle;
    private readonly List<StrokeView> _targetWireframes = [];

    public override void Start(CursorButtonData data)
    {
        Document.Get<WorldBody>().DefaultCursorShape = Control.CursorShape.Cross;
        _brushCircle = new StrokeView { Material = AutoloadRendering.DashWireframeMaterial };
        Document.Get<WorldOverlay>().AddChild(_brushCircle);
        UpdateBrushCircle(data.WorldPosition);

        ShowTargetWireframes();
    }

    public override void Moving(CursorMotionData data)
    {
        UpdateBrushCircle(data.WorldPosition);
    }

    public override void End(CursorButtonData data) => Cancel();

    public override void Cancel()
    {
        _brushCircle?.QueueFree();
        _brushCircle = null;

        _targetWireframes.ForEach(w => w.QueueFree());
        _targetWireframes.Clear();

        Document.Get<WorldBody>().DefaultCursorShape = default;
    }

    public override bool OnKey(InputEventKey key, CursorButtonData data) => false;

    private void UpdateBrushCircle(Vector2 center)
    {
        var liquifyTool = Tool;
        _brushCircle.SetGeometry(CreateCircle(center, liquifyTool.Radius.Value, 64), AppPreference.StrokeWireframeRadius);
    }

    private void ShowTargetWireframes()
    {
        var targets = LiquifyTargetScope.Resolve(Document, SelectedLayers);
        var worldOverlay = Document.Get<WorldOverlay>();

        foreach (var e in targets)
        {
            var geom = e.Get<SampledPolyline>();
            var wireframe = new StrokeView { Material = AutoloadRendering.WireframeMaterial };
            wireframe.SetInstanceShaderParameter("overridingColor", AppPreference.StrokeWireframeColor);
            wireframe.SetGeometry(geom.Positions.Value, AppPreference.StrokeWireframeRadius);
            worldOverlay.AddChild(wireframe);
            _targetWireframes.Add(wireframe);
        }
    }

    private static Vector2[] CreateCircle(Vector2 center, float radius, int segmentCount)
    {
        var points = new Vector2[segmentCount + 1];
        for (int i = 0; i <= segmentCount; i++)
        {
            float angle = Mathf.Tau * i / segmentCount;
            points[i] = center + Vector2.Right.Rotated(angle) * radius;
        }
        return points;
    }
}
