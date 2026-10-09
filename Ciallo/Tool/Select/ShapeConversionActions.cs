using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Ciallo.Command;
using Ciallo.Data;
using Frent;
using Godot;

namespace Ciallo.Tool;

public static class ShapeConversionActions
{
    public static bool CanConvert(Entity layer, IReadOnlyCollection<Entity> selected) =>
        selected.Count > 0
        && selected.All(shape => shape.Get<LayerTreeNode>().ParentValue == layer)
        && (selected.All(shape => shape.Has<StrokeSetting>())
            || selected.All(shape => shape.Has<FilledPolygonSetting>()));

    public static bool Apply(Entity layer)
    {
        var manager = layer.Document.Get<SelectionManager>();
        var selection = manager.SelectedShapes;
        Entity[] before = [.. selection];
        if (!CanConvert(layer, before)) return false;
        bool toStroke = before[0].Has<FilledPolygonSetting>();
        var brush = toStroke ? manager.WorkingStrokeBrush.Value : manager.WorkingVectorFillBrush.Value;
        if (brush.IsNull) return false;

        // Normalize only to discover real boundaries/empty regions. Stroke-to-fill keeps
        // the original path positions, including self-intersections, just like Paint Fill.
        var replacements = new Dictionary<Entity, ImmutableArray<ImmutableArray<Vector2>>>();
        foreach (var source in before)
        {
            var positions = source.Get<SampledPolyline>().Positions.Value;
            if (positions.Length < 3) continue;
            var contours = Geometry2D.MergePolygons(System.Array.Empty<Vector2>(), positions.AsSpan());
            if (contours.Count == 0) continue;
            replacements.Add(source, toStroke
                ? [.. contours.Select(ring => ImmutableArray.CreateRange(ring.Append(ring[0])))]
                : [positions[0] == positions[^1] ? positions : positions.Add(positions[0])]);
        }
        if (replacements.Count == 0) return false;

        float radius = toStroke
            ? brush.Get<StrokeBrushSetting>().ToRadiusSampler()(1f)
            : AppPreference.StrokeWireframeRadius;
        var results = new Dictionary<Entity, List<Entity>>();
        var command = new CommandBuilder(toStroke ? "Convert to strokes" : "Convert to filled polygons", layer);
        command.Commands.Add(new DelegateCommand(selection.Clear, () => SetSelection(before)));
        foreach (var (source, rings) in replacements.OrderByDescending(pair => pair.Key.Get<LayerTreeNode>().Index))
        {
            int index = source.Get<LayerTreeNode>().Index;
            command.SetTarget(source).RemoveFromLayerTree().DeleteShape();
            List<Entity> converted = [];
            results.Add(source, converted);
            foreach (var ring in rings)
            {
                var result = layer.World.Create();
                converted.Add(result);
                command.SetTarget(result);
                if (toStroke)
                    command.NewStroke().SetProperty(shape => shape.Get<StrokeSetting>().Brush, brush);
                else
                    command.NewFilledPolygon().SetProperty(shape => shape.Get<FilledPolygonSetting>().BrushE, brush);
                command.AddToLayerTree(layer, index++).SetSampledPolyline(ring,
                    [.. Enumerable.Repeat(radius, ring.Length)],
                    [.. Enumerable.Repeat(1f, ring.Length)],
                    [.. Enumerable.Repeat(Vector2.Zero, ring.Length)]);
            }
        }
        Entity[] after = [.. before.SelectMany(source => results.TryGetValue(source, out var converted)
            ? (IEnumerable<Entity>)converted : [source])];
        command.Commands.Add(new DelegateCommand(() => SetSelection(after), selection.Clear));
        command.Commit();
        return true;

        void SetSelection(IEnumerable<Entity> shapes)
        {
            selection.Clear();
            selection.AddRange(shapes);
        }
    }
}
