using System.Collections.Immutable;
using Ciallo.Command;
using Ciallo.Data;
using Ciallo.Geometry;
using Ciallo.Rendering;
using Frent;
using Godot;

namespace Ciallo.Tool;

[RegisterState]
public class PaintFillInteractor : CapturingInteraction
{
    private readonly PolylineInteractiveGenerator _generator = new()
    {
        Mode = PolylineInteractiveGenerator.RadiusMode.Fixed,
        FixedRadius = AppPreference.StrokeWireframeRadius,
    };
    private StrokeView _dashPreview;
    private Entity _fillBrush;

    public override void BeforeSourceExit(Interaction session)
    {
        _fillBrush = Document.Get<SelectionManager>().WorkingVectorFillBrush.Value;
    }

    public override void Start(CursorButtonData data)
    {
        _generator.Start(data);

        _dashPreview = new StrokeView();
        _dashPreview.Material = AutoloadRendering.DashWireframeMaterial;
        var layerView = WorkingLayer.Get<ShapeLayerView>();
        layerView.AddChild(_dashPreview);
    }

    public override void Moving(CursorMotionData data)
    {
        _generator.Update(data);
        var geometry = _generator.CurrentGeometry;
        ImmutableArray<Vector2> points = [.. geometry.Positions, geometry.Positions[0]];
        _dashPreview.SetGeometry(points, AppPreference.StrokeWireframeRadius);
    }

    public override void End(CursorButtonData data)
    {
        _generator.End(data);
        var geometry = _generator.CurrentGeometry;
        if (geometry.Count < 3)
        {
            Clear();
            return;
        }
        new CommandBuilder("Paint Fill", WorkingLayer.World.Create())
            .NewFilledPolygon()
            .AddToLayerTree(WorkingLayer)
            .SetSampledPolyline(
                [.. geometry.Positions, geometry.Positions[0]],
                [.. geometry.Radii, geometry.Radii[0]],
                [.. geometry.Pressures, geometry.Pressures[0]],
                [.. geometry.Tilts, geometry.Tilts[0]])
            .SetProperty(e => e.Get<FilledPolygonSetting>().BrushE, _fillBrush)
            .Commit();
        Clear();
    }

    public override void Cancel()
    {
        Clear();
    }

    public void Clear()
    {
        _generator.Clear();
        _dashPreview?.QueueFree();
        _dashPreview = null;
    }
}
