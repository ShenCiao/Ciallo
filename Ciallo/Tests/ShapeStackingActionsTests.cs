using System.Linq;
using System.Threading.Tasks;
using Ciallo.Command;
using Ciallo.Data;
using Ciallo.Tool;
using Frent;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace Ciallo.Tests;

[TestSuite, RequireGodotRuntime]
public class ShapeStackingActionsTests
{
    private Entity _document;

    [AfterTest]
    public async Task TearDown()
    {
        AppDocumentManager.Remove(_document);
        var tree = (SceneTree)Engine.GetMainLoop();
        await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
    }

    [TestCase(true)]
    [TestCase(false)]
    public void NonAdjacentSelectionKeepsBothRelativeOrdersAndUndoesAsOneAction(bool toTop)
    {
        _document = AppDocumentManager.Create(new DocumentSetting());
        var layer = _document.World.Create();
        layer.Add(new LayerTreeNode());
        _document.Get<LayerTreeNode>().AddChild(layer);
        Entity[] shapes = [.. Enumerable.Range(0, 5).Select(_ =>
        {
            var shape = _document.World.Create();
            shape.Add(new LayerTreeNode());
            layer.Get<LayerTreeNode>().AddChild(shape);
            return shape;
        })];
        var selection = _document.Get<SelectionManager>().SelectedShapes;
        selection.AddRange([shapes[3], shapes[1]]); // Click order differs from stacking order.
        Entity[] expected = toTop
            ? [shapes[0], shapes[2], shapes[4], shapes[1], shapes[3]]
            : [shapes[1], shapes[3], shapes[0], shapes[2], shapes[4]];
        var history = _document.Get<CommandManager>();

        AssertThat(ShapeStackingActions.MoveSelection(layer, toTop)).IsTrue();
        AssertThat(layer.Get<LayerTreeNode>().Children.ToArray()).ContainsExactly(expected);
        AssertThat(selection.ToArray()).ContainsExactly(shapes[3], shapes[1]);
        AssertThat(ShapeStackingActions.MoveSelection(layer, toTop)).IsFalse();
        history.Undo();
        AssertThat(layer.Get<LayerTreeNode>().Children.ToArray()).ContainsExactly(shapes);
        AssertThat(history.HasUndo).IsFalse();
        history.Redo();
        AssertThat(layer.Get<LayerTreeNode>().Children.ToArray()).ContainsExactly(expected);
        AssertThat(selection.ToArray()).ContainsExactly(shapes[3], shapes[1]);
    }
}
