using System.Linq;
using Ciallo.Command;
using Ciallo.Data;
using Frent;

namespace Ciallo.Tool;

public static class ShapeStackingActions
{
    public static bool MoveSelection(Entity document, bool toTop)
    {
        var selected = document.Get<SelectionManager>().SelectedShapes.ToHashSet();
        var command = new CommandBuilder(toTop ? "Bring shapes to front" : "Send shapes to back", document);
        foreach (var layer in selected.Select(shape => shape.Get<LayerTreeNode>().ParentValue).Distinct())
        {
            var children = layer.Get<LayerTreeNode>().Children.ToArray();
            var moving = children.Where(selected.Contains).ToArray();
            var remaining = children.Where(shape => !selected.Contains(shape));
            var desired = toTop ? remaining.Concat(moving) : moving.Concat(remaining);
            if (children.SequenceEqual(desired)) continue;

            // Visit toward the destination to preserve the selected order within each layer.
            foreach (var shape in toTop ? moving : moving.Reverse())
                command.MoveLayer(shape, layer, toTop ? children.Length - 1 : 0);
        }
        if (command.Commands.Count == 0) return false;
        command.Commit();
        return true;
    }
}
