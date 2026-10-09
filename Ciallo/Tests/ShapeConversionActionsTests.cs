using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Ciallo.Command;
using Ciallo.Data;
using Ciallo.Geometry;
using Ciallo.Rendering;
using Ciallo.Tool;
using Frent;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace Ciallo.Tests;

[TestSuite, RequireGodotRuntime]
public class ShapeConversionActionsTests
{
    private Entity _document;
    private Entity _layer;
    private Entity _strokeBrush;
    private Entity _fillBrush;

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
        _strokeBrush = _document.World.Create();
        _strokeBrush.Add(new StrokeBrushSetting());
        _strokeBrush.Get<StrokeBrushSetting>().BaseRadius.Value = 7f;
        _strokeBrush.Get<StrokeBrushSetting>().ActiveBrushFlags.Value = BrushFlags.Pressure2Radius;
        _fillBrush = _document.World.Create();
        _fillBrush.Add(new FillBrushSetting());
        var selection = _document.Get<SelectionManager>();
        selection.WorkingStrokeBrush.Value = _strokeBrush;
        selection.WorkingVectorFillBrush.Value = _fillBrush;
    }

    [AfterTest]
    public async Task TearDown()
    {
        AppDocumentManager.Remove(_document);
        var tree = (SceneTree)Engine.GetMainLoop();
        await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
    }

    [TestCase]
    public void HoledPolygonSplitsWithoutBridgesAndRestoresStackingSelectionAndBrushSnapshot()
    {
        var outer = Rectangle(0, 0, 20, 20);
        var hole = Rectangle(5, 5, 15, 15);
        var ring = new System.Collections.Generic.List<System.Collections.Generic.IReadOnlyList<Vector2>>
            { outer, hole.Reverse().ToArray() }.ConnectHoles();
        var bottom = AddShape([.. ring, ring[0]], false);
        var untouched = AddShape(Rectangle(30, 0, 40, 10), false);
        var top = AddShape(Rectangle(50, 0, 60, 10), false);
        var manager = _document.Get<SelectionManager>();
        manager.SelectedShapes.AddRange([top, bottom]);
        var history = _document.Get<CommandManager>();

        AssertThat(ShapeConversionActions.Apply(_layer)).IsTrue();
        var after = manager.SelectedShapes.ToArray();
        AssertThat(after.Length).IsEqual(3);
        AssertThat(_layer.Get<LayerTreeNode>().Children.ToArray()).ContainsExactly(after[1], after[2], untouched, after[0]);
        float expectedRadius = _strokeBrush.Get<StrokeBrushSetting>().ToRadiusSampler()(1f);
        foreach (var shape in after)
        {
            var geometry = shape.Get<SampledPolyline>();
            AssertThat(shape.Get<StrokeSetting>().Brush.Value).IsEqual(_strokeBrush);
            AssertThat(geometry.Positions.Value[0]).IsEqual(geometry.Positions.Value[^1]);
            AssertThat(geometry.Pressures.Value.All(p => p == 1f)).IsTrue();
            AssertThat(geometry.Radii.Value.All(r => r == expectedRadius)).IsTrue();
        }
        // Total perimeter is 80 + 40: no out-and-back bridge is stroked.
        float perimeter = after.Skip(1).Sum(shape =>
        {
            var points = shape.Get<SampledPolyline>().Positions.Value;
            return Enumerable.Range(1, points.Length - 1).Sum(i => points[i].DistanceTo(points[i - 1]));
        });
        AssertThat(Mathf.Abs(perimeter - 120f) < 0.001f).IsTrue();

        history.Undo();
        AssertThat(_layer.Get<LayerTreeNode>().Children.ToArray()).ContainsExactly(bottom, untouched, top);
        AssertThat(manager.SelectedShapes.ToArray()).ContainsExactly(top, bottom);
        AssertThat(history.HasUndo).IsFalse();
        manager.WorkingStrokeBrush.Value = Entity.Null;
        _strokeBrush.Get<StrokeBrushSetting>().BaseRadius.Value = 20f;
        history.Redo();
        AssertThat(manager.SelectedShapes.ToArray()).ContainsExactly(after);
        AssertThat(_layer.Get<LayerTreeNode>().Children.ToArray()).ContainsExactly(after[1], after[2], untouched, after[0]);
        AssertThat(after.All(shape => shape.Get<SampledPolyline>().Radii.Value.All(r => r == expectedRadius))).IsTrue();
        RegisterHistoryNodesForCleanup();
    }

    [TestCase]
    public void SelfIntersectingStrokeClosesWhileDegenerateSourcesSurviveBatchAndUndo()
    {
        ImmutableArray<Vector2> bowTie = [new(0, 0), new(10, 10), new(0, 10), new(10, 0)];
        var valid = AddShape(bowTie, true);
        var point = AddShape([new(20, 0)], true);
        var line = AddShape([new(30, 0), new(35, 0), new(40, 0)], true);
        var manager = _document.Get<SelectionManager>();
        manager.SelectedShapes.AddRange([line, valid, point]);
        var history = _document.Get<CommandManager>();

        AssertThat(ShapeConversionActions.Apply(_layer)).IsTrue();
        var fill = manager.SelectedShapes[1];
        AssertThat(fill.Get<FilledPolygonSetting>().BrushE.Value).IsEqual(_fillBrush);
        AssertThat(fill.Get<SampledPolyline>().Positions.Value.ToArray()).ContainsExactly(bowTie.Add(bowTie[0]).ToArray());
        AssertThat(manager.SelectedShapes.ToArray()).ContainsExactly(line, fill, point);
        AssertThat(_layer.Get<LayerTreeNode>().Children.ToArray()).ContainsExactly(fill, point, line);
        // The skipped strokes make the resulting selection mixed, hence non-convertible.
        AssertThat(ShapeConversionActions.CanConvert(_layer, manager.SelectedShapes.ToArray())).IsFalse();
        AssertThat(ShapeConversionActions.Apply(_layer)).IsFalse();
        history.Undo();
        AssertThat(manager.SelectedShapes.ToArray()).ContainsExactly(line, valid, point);
        AssertThat(_layer.Get<LayerTreeNode>().Children.ToArray()).ContainsExactly(valid, point, line);
        AssertThat(history.HasUndo).IsFalse();
        history.Redo();
        AssertThat(manager.SelectedShapes.ToArray()).ContainsExactly(line, fill, point);
        history.Undo();
        manager.SelectedShapes.Remove(valid);
        AssertThat(ShapeConversionActions.Apply(_layer)).IsFalse();
        AssertThat(history.HasUndo).IsFalse();
        RegisterHistoryNodesForCleanup();
    }

    private void RegisterHistoryNodesForCleanup()
    {
        // Register before teardown, as in PolygonBooleanActionsTests: history keeps
        // removed shapes detached, outside the document's node hierarchy.
        foreach (var shape in _document.World.CreateQuery().With<SampledPolyline>().Build().EnumerateWithEntities())
        {
            if (shape.Has<StrokeSetting>()) AutoFree(shape.Get<StrokeView>());
            else AutoFree(shape.Get<Polygon2D>());
            AutoFree(shape.Get<PolylineWireframe>());
            AutoFree(shape.Get<Body>());
        }
    }

    private Entity AddShape(ImmutableArray<Vector2> points, bool stroke)
    {
        var shape = _document.World.Create();
        var command = new CommandBuilder("Fixture", shape);
        if (stroke) command.NewStroke().SetProperty(e => e.Get<StrokeSetting>().Brush, _strokeBrush);
        else command.NewFilledPolygon().SetProperty(e => e.Get<FilledPolygonSetting>().BrushE, _fillBrush);
        command.AddToLayerTree(_layer).SetSampledPolyline(points,
            [.. Enumerable.Repeat(2f, points.Length)],
            [.. Enumerable.Repeat(0.25f, points.Length)],
            [.. Enumerable.Repeat(Vector2.Zero, points.Length)]).Do();
        return shape;
    }

    private static ImmutableArray<Vector2> Rectangle(float x0, float y0, float x1, float y1) =>
        [new(x0, y0), new(x1, y0), new(x1, y1), new(x0, y1), new(x0, y0)];
}
