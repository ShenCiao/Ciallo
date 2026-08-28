using System.Linq;
using Ciallo.Command;
using Ciallo.Data;
using Godot;

namespace Ciallo.Tool;

[RegisterState]
public class PaintStrokeOnVectorFill : PaintStrokeInteractor
{
    public override void End(CursorButtonData data)
    {
        var geometry = BuildCommitGeometry(data);
        var layers = WorkingLayer.Get<VectorFillLayerSetting>().ReferenceLayers;
        if (layers.Count > 0)
        {
            var targetShapeLayer = layers.First();
            new CommandBuilder("Paint Stroke", WorkingLayer.World.Create())
                .NewStroke()
                .AddToLayerTree(targetShapeLayer)
                .SetProperty(e => e.Get<StrokeSetting>().Brush, BrushE)
                .SetSampledPolyline(geometry.Positions, geometry.Radii, geometry.Pressures, geometry.Tilts)
                .Commit();
        }
        Clear();
    }

    public override bool OnKey(InputEventKey key, CursorButtonData data) => true;
}
