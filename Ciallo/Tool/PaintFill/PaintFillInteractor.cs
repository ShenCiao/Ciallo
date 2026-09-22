using System.Collections.Immutable;
using System.Linq;
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
    private readonly PolylineInteractiveGenerator _generator = new();
    private StrokeView _dashPreview;
    private Entity _fillBrush;

    public override void BeforeSourceExit(Interaction session)
    {
        _fillBrush = Document.Get<SelectionManager>().WorkingVectorFillBrush.Value;
    }

    public override void Start(CursorButtonData data)
    {
        _generator.Start(data);

        _dashPreview = CreatePreview(PrimaryLayer);
    }

    internal static StrokeView CreatePreview(Entity layer)
    {
        var preview = new StrokeView { Material = AutoloadRendering.DashWireframeMaterial };
        layer.Get<ShapeLayerView>().AddChild(preview);
        return preview;
    }

    public override void Moving(CursorMotionData data)
    {
        _generator.Update(data);
        var geometry = _generator.CurrentSamples;
        _dashPreview.SetGeometry(geometry.Positions, AppPreference.StrokeWireframeRadius);
    }

    public override void End(CursorButtonData data)
    {
        _generator.End(data);
        var geometry = _generator.CurrentSamples;
        if (geometry.Count >= 3)
            CommitPolygon(PrimaryLayer, _fillBrush, new PolylineSamples(
                [.. geometry.Positions, geometry.Positions[0]],
                [.. geometry.Pressures, geometry.Pressures[0]],
                [.. geometry.Tilts, geometry.Tilts[0]]));
        Clear();
    }

    internal static void CommitPolygon(Entity layer, Entity brush, PolylineSamples geometry)
    {
        // Signed area would also reject valid self-intersecting rings (e.g. a figure eight).
        if (!HasNonCollinearPoints(geometry)) return;
        new CommandBuilder("Paint Fill", layer.World.Create())
            .NewFilledPolygon()
            .AddToLayerTree(layer)
            .SetSampledPolyline(
                geometry.Positions.ToImmutableArray(),
                Enumerable.Repeat(AppPreference.StrokeWireframeRadius, geometry.Count).ToImmutableArray(),
                geometry.Pressures.ToImmutableArray(),
                geometry.Tilts.ToImmutableArray())
            .SetProperty(e => e.Get<FilledPolygonSetting>().BrushE, brush)
            .Commit();
    }

    private static bool HasNonCollinearPoints(PolylineSamples geometry)
    {
        Vector2 origin = geometry.Positions[0];
        Vector2 direction = Vector2.Zero;
        foreach (var point in geometry.Positions)
        {
            Vector2 offset = point - origin;
            if (offset.LengthSquared() > direction.LengthSquared())
                direction = offset;
        }
        // Cubic sampling introduces float roundoff even along an exactly straight line.
        float tolerance = 1e-6f * direction.LengthSquared();
        return geometry.Positions.Any(point => Mathf.Abs(direction.Cross(point - origin)) > tolerance);
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
