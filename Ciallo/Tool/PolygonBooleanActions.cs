using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Ciallo.Command;
using Ciallo.Data;
using Ciallo.Geometry;
using Frent;
using Godot;
using Operation = Godot.Geometry2D.PolyBooleanOperation;

namespace Ciallo.Tool;

public static class PolygonBooleanActions
{
    public static string Label(Operation? operation) => operation switch
    {
        null => "New polygon",
        Operation.Union => "Union",
        Operation.Difference => "Subtract",
        Operation.Intersection => "Intersect",
        Operation.Xor => "Exclude",
        _ => throw new ArgumentOutOfRangeException(nameof(operation)),
    };

    public static bool CanApplySelection(Entity layer, IReadOnlyCollection<Entity> selected) =>
        selected.Count >= 2 && selected.All(shape => shape.Has<FilledPolygonSetting>()
            && shape.Get<LayerTreeNode>().ParentValue == layer);

    public static bool ApplySelection(Entity layer, Operation operation)
    {
        var selected = layer.Document.Get<SelectionManager>().SelectedShapes.ToArray();
        if (!CanApplySelection(layer, selected)) return false;
        var sources = selected.OrderBy(shape => shape.Get<LayerTreeNode>().Index).ToArray();
        var result = PolygonBoolean.Apply(sources.Select(Positions), operation);
        Commit(layer, Label(operation), [new(sources, result, Brush(sources[0]))], selectResults: true);
        return true;
    }

    public static void Paint(Entity layer, Entity brush, ImmutableArray<Vector2> ring, Operation operation)
    {
        var selection = layer.Document.Get<SelectionManager>().SelectedShapes;
        var selectedPolygons = selection.Where(shape => shape.Has<FilledPolygonSetting>()
            && shape.Get<LayerTreeNode>().ParentValue == layer).ToHashSet();
        var targets = layer.Get<LayerTreeNode>().Children.Where(shape => shape.Has<FilledPolygonSetting>()
            && Brush(shape) == brush
            && (selectedPolygons.Count == 0 || selectedPolygons.Contains(shape))
            && PolygonBoolean.Overlaps(Positions(shape), ring)).ToArray();

        if (targets.Length == 0 && operation is Operation.Difference or Operation.Intersection) return;

        List<Replacement> replacements = [];
        if (operation is Operation.Difference or Operation.Intersection)
        {
            foreach (var target in targets)
                replacements.Add(new([target], PolygonBoolean.Apply([Positions(target), ring], operation), brush));
        }
        else
        {
            // Existing overlapping paint is one filled region; XOR only toggles it against
            // the new gesture, rather than toggling existing polygons against each other.
            var existing = PolygonBoolean.Apply(targets.Select(Positions), Operation.Union);
            var result = PolygonBoolean.Apply(existing.Append(ring), operation);
            if (targets.Length == 0 && result.IsEmpty) return;
            replacements.Add(new(targets, result, brush));
        }
        Commit(layer, $"Paint Fill {Label(operation)}", replacements, selectedPolygons.Count > 0);
    }

    private sealed record Replacement(Entity[] Sources, ImmutableArray<ImmutableArray<Vector2>> Polygons, Entity Brush);

    private static ImmutableArray<Vector2> Positions(Entity shape) => shape.Get<SampledPolyline>().Positions.Value;
    private static Entity Brush(Entity shape) => shape.Get<FilledPolygonSetting>().BrushE.Value;

    private static void Commit(Entity layer, string action, IReadOnlyList<Replacement> replacements, bool selectResults)
    {
        var selection = layer.Document.Get<SelectionManager>().SelectedShapes;
        Entity[] before = [.. selection];
        var removed = replacements.SelectMany(replacement => replacement.Sources).ToHashSet();
        List<Entity> after = [.. before.Where(shape => !removed.Contains(shape))];
        var command = new CommandBuilder(action, layer);

        // Restore selection only after undo has restored its objects and their tree positions.
        command.Commands.Add(new DelegateCommand(selection.Clear, () => SetSelection(before)));
        foreach (var replacement in replacements.OrderByDescending(InsertionIndex))
        {
            int index = InsertionIndex(replacement);
            foreach (var source in replacement.Sources.OrderByDescending(shape => shape.Get<LayerTreeNode>().Index))
                command.SetTarget(source).RemoveFromLayerTree().DeleteShape();
            foreach (var polygon in replacement.Polygons)
            {
                var result = layer.World.Create();
                ImmutableArray<float> radii = [.. Enumerable.Repeat(AppPreference.StrokeWireframeRadius, polygon.Length)];
                ImmutableArray<float> pressures = [.. Enumerable.Repeat(1f, polygon.Length)];
                ImmutableArray<Vector2> tilts = [.. Enumerable.Repeat(Vector2.Zero, polygon.Length)];
                command.SetTarget(result).NewFilledPolygon().AddToLayerTree(layer, index++)
                    .SetSampledPolyline(polygon, radii, pressures, tilts)
                    .SetProperty(shape => shape.Get<FilledPolygonSetting>().BrushE, replacement.Brush);
                if (selectResults) after.Add(result);
            }
        }
        command.Commands.Add(new DelegateCommand(() => SetSelection(after), selection.Clear));
        command.Commit();
        return;

        int InsertionIndex(Replacement replacement) => replacement.Sources.Length == 0
            ? layer.Get<LayerTreeNode>().Children.Count
            : replacement.Sources.Min(shape => shape.Get<LayerTreeNode>().Index);

        void SetSelection(IEnumerable<Entity> shapes)
        {
            selection.Clear();
            selection.AddRange(shapes);
        }
    }
}
