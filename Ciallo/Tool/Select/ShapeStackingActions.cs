using System.Linq;
using Ciallo.Command;
using Ciallo.Data;
using Frent;

namespace Ciallo.Tool;

public static class ShapeStackingActions
{
    public static bool MoveSelection(Entity layer, bool toTop)
    {
        var selected = layer.Document.Get<SelectionManager>().SelectedShapes.ToHashSet();
        var children = layer.Get<LayerTreeNode>().Children.ToArray();
        var moving = children.Where(selected.Contains).ToArray();
        var remaining = children.Where(shape => !selected.Contains(shape));
        var desired = toTop ? remaining.Concat(moving) : moving.Concat(remaining);
        if (children.SequenceEqual(desired)) return false;

        var command = new CommandBuilder(toTop ? "Bring shapes to front" : "Send shapes to back", layer);
        // Moving to an extreme shifts the remaining indices. Visit bottom-to-top
        // for front, and top-to-bottom for back, to preserve the selected order.
        foreach (var shape in toTop ? moving : moving.Reverse())
            command.MoveLayer(shape, layer, toTop ? children.Length - 1 : 0);
        command.Commit();
        return true;
    }
}
