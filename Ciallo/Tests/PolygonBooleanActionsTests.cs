using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Ciallo.Command;
using Ciallo.Data;
using Ciallo.Rendering;
using Ciallo.Tool;
using Frent;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;
using Operation = Godot.Geometry2D.PolyBooleanOperation;

namespace Ciallo.Tests;

[TestSuite, RequireGodotRuntime]
public class PolygonBooleanActionsTests
{
    private Entity _document;
    private Entity _layer;
    private Entity _brush;
    private Entity _otherBrush;

    [BeforeTest]
    public void SetUp()
    {
        _document = AppDocumentManager.Create(new DocumentSetting());
        _layer = _document.World.Create();
        _layer.Add(new LayerTreeNode());
        _layer.Add(new ShapeLayerSetting());
        _layer.AddNode(new ShapeLayerView());
        _layer.AddNode(new OverlayHolder());
        _layer.AddNode(new BodyHolder());
        _document.Get<LayerTreeNode>().AddChild(_layer);
        _brush = _document.World.Create();
        _brush.Add(new FillBrushSetting());
        _otherBrush = _document.World.Create();
        _otherBrush.Add(new FillBrushSetting());
    }

    [AfterTest]
    public async Task TearDown()
    {
        AppDocumentManager.Remove(_document);
        await FlushQueuedNodes();
    }

    private static async Task FlushQueuedNodes()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
    }

    [TestCase]
    public void SplitAndEmptyResultsUndoObjectsStackingBrushesAndSelectionTogether()
    {
        var bottom = AddPolygon(Rectangle(0, 0, 10, 10), _brush);
        var untouched = AddPolygon(Rectangle(30, 0, 40, 10), _otherBrush);
        var cutter = AddPolygon(Rectangle(4, -1, 6, 11), _otherBrush);
        var selection = _document.Get<SelectionManager>().SelectedShapes;
        // Opposite to stacking order: operand direction must not depend on click order.
        selection.AddRange([cutter, bottom]);
        var history = _document.Get<CommandManager>();
        AssertThat(PolygonBooleanActions.ApplySelection(_layer, Operation.Difference)).IsTrue();
        var split = selection.ToArray();
        AssertThat(split.Length).IsEqual(2);
        CheckRenderedArea([.. split.Select(Positions)], 80);
        AssertThat(split.All(shape => shape.Get<FilledPolygonSetting>().BrushE.Value == _brush)).IsTrue();
        AssertThat(_layer.Get<LayerTreeNode>().Children.ToArray()).ContainsExactly(split[0], split[1], untouched);

        history.Undo();
        AssertThat(_layer.Get<LayerTreeNode>().Children.ToArray()).ContainsExactly(bottom, untouched, cutter);
        AssertThat(selection.ToArray()).ContainsExactly(cutter, bottom);
        AssertThat(history.HasUndo).IsFalse();
        history.Redo();
        AssertThat(selection.ToArray()).ContainsExactly(split);
        AssertThat(_layer.Get<LayerTreeNode>().Children.ToArray()).ContainsExactly(split[0], split[1], untouched);

        PolygonBooleanActions.ApplySelection(_layer, Operation.Intersection);
        AssertThat(selection.Count).IsEqual(0);
        AssertThat(_layer.Get<LayerTreeNode>().Children.ToArray()).ContainsExactly(untouched);
        history.Undo();
        AssertThat(selection.ToArray()).ContainsExactly(split);
        CheckRenderedArea([.. selection.Select(Positions)], 80);
        RegisterHistoryNodesForCleanup();
    }

    [TestCase]
    public async Task PaintXorUnionsExistingCoverageAndPreservesOtherBrushes()
    {
        var left = AddPolygon(Rectangle(0, 0, 10, 10), _brush);
        var other = AddPolygon(Rectangle(0, 0, 15, 10), _otherBrush);
        var right = AddPolygon(Rectangle(5, 0, 15, 10), _brush);
        var history = _document.Get<CommandManager>();
        var selection = _document.Get<SelectionManager>().SelectedShapes;
        PolygonBooleanActions.Paint(_layer, _brush, Rectangle(7, -1, 8, 11), Operation.Xor);
        var results = _layer.Get<LayerTreeNode>().Children.Where(shape => shape != other).ToArray();
        CheckRenderedArea([.. results.Select(Positions)], 142);
        AssertThat(results.Length).IsEqual(4);
        AssertThat(selection.Count).IsEqual(0);
        AssertThat(_layer.Get<LayerTreeNode>().Children.Last()).IsEqual(other);
        AssertThat(results.All(shape => shape.Get<FilledPolygonSetting>().BrushE.Value == _brush)).IsTrue();
        history.Undo();
        AssertThat(_layer.Get<LayerTreeNode>().Children.ToArray()).ContainsExactly(left, other, right);

        // An explicit polygon selection restricts the gesture, without changing an unselected sibling.
        selection.Add(right);
        PolygonBooleanActions.Paint(_layer, _brush, Rectangle(6, -1, 9, 11), Operation.Difference);
        AssertThat(selection.Count).IsEqual(2);
        CheckRenderedArea([.. selection.Select(Positions)], 70);
        AssertThat(_layer.Get<LayerTreeNode>().Children.Take(2).ToArray()).ContainsExactly(left, other);
        history.Undo();
        AssertThat(selection.ToArray()).ContainsExactly(right);
        AssertThat(_layer.Get<LayerTreeNode>().Children.ToArray()).ContainsExactly(left, other, right);
        RegisterHistoryNodesForCleanup();
        await FlushQueuedNodes(); // The new action discarded the old redo branch's entities.
    }

    private void RegisterHistoryNodesForCleanup()
    {
        // History intentionally retains detached shape nodes for undo. Let the test runner
        // release them before its orphan check; entity teardown also tolerates freed nodes.
        foreach (var shape in _document.World.CreateQuery().With<FilledPolygonSetting>().Build().EnumerateWithEntities())
        {
            AutoFree(shape.Get<Polygon2D>());
            AutoFree(shape.Get<PolylineWireframe>());
            AutoFree(shape.Get<Body>());
        }
    }

    private Entity AddPolygon(ImmutableArray<Vector2> ring, Entity brush)
    {
        var shape = _document.World.Create();
        new CommandBuilder("Fixture", shape).NewFilledPolygon().AddToLayerTree(_layer)
            .SetSampledPolyline(ring, [.. Enumerable.Repeat(1f, ring.Length)],
                [.. Enumerable.Repeat(1f, ring.Length)], [.. Enumerable.Repeat(Vector2.Zero, ring.Length)])
            .SetProperty(entity => entity.Get<FilledPolygonSetting>().BrushE, brush).Do();
        return shape;
    }

    private static ImmutableArray<Vector2> Positions(Entity shape) => shape.Get<SampledPolyline>().Positions.Value;

    private static ImmutableArray<Vector2> Rectangle(float x0, float y0, float x1, float y1) =>
        [new(x0, y0), new(x1, y0), new(x1, y1), new(x0, y1), new(x0, y0)];

    private static void CheckRenderedArea(ImmutableArray<ImmutableArray<Vector2>> polygons, double expected)
    {
        double area = 0;
        foreach (var polygon in polygons)
        {
            AssertThat(polygon[0]).IsEqual(polygon[^1]);
            using var mesh = Arrangement2D.RepairAndTriangulate([polygon.ToArray()]);
            var vertices = (Vector2[])mesh["vertices"];
            var triangles = (int[])mesh["indices"];
            for (int i = 0; i < triangles.Length; i += 3)
            {
                var a = vertices[triangles[i]];
                var b = vertices[triangles[i + 1]];
                var c = vertices[triangles[i + 2]];
                area += Math.Abs(((double)b.X - a.X) * ((double)c.Y - a.Y)
                    - ((double)b.Y - a.Y) * ((double)c.X - a.X)) * 0.5;
            }
        }
        AssertThat(Math.Abs(area - expected) < 0.001).IsTrue();
    }
}
