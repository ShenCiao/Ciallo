using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Ciallo.Command;
using Ciallo.Data;
using Frent;

namespace Ciallo.GuiControl;

internal static partial class LayerContextActions
{
    // Bottom-to-top tree order, with selected descendants covered by their selected ancestor.
    public static ImmutableArray<Entity> OperationRoots(ImmutableArray<Entity> layers)
    {
        if (layers.IsEmpty) return [];
        var selected = layers.ToHashSet();
        return [.. LayerOrder(layers[0].Document).Where(e => selected.Contains(e)
            && !e.Get<LayerTreeNode>().EnumerateAncestors().Any(selected.Contains))];
    }

    private static IEnumerable<Entity> LayerOrder(Entity parent)
    {
        foreach (var layer in parent.Get<LayerTreeNode>().GetLayerChildren())
        {
            yield return layer;
            if (!layer.Has<FolderLayerSetting>()) continue;
            foreach (var descendant in LayerOrder(layer))
                yield return descendant;
        }
    }

    private static bool CoveredBy(Entity layer, IReadOnlySet<Entity> roots) => roots.Contains(layer)
        || layer.Get<LayerTreeNode>().EnumerateAncestors().Any(roots.Contains);

    public static void DeleteLayers(ImmutableArray<Entity> layers)
    {
        if (layers.IsEmpty) return;
        var roots = OperationRoots(layers);
        var deleted = roots.ToHashSet();
        var document = layers[0].Document;
        var selected = document.Get<SelectionManager>().SelectedLayers.Value;
        ImmutableArray<Entity> survivors = [.. selected.Where(e => !CoveredBy(e, deleted))];
        if (survivors.IsEmpty)
        {
            var order = LayerOrder(document).ToList();
            int index = order.IndexOf(layers[0]);
            var focus = order.Skip(index + 1).Concat(order.Take(index).Reverse())
                .FirstOrDefault(e => !CoveredBy(e, deleted));
            survivors = focus.IsNull ? [] : [focus];
        }

        var cmd = new CommandBuilder("Delete Layers", document).SetLayerSelection(layers: survivors);
        foreach (var layer in roots.Reverse())
        {
            RemoveParentCelFolderExposures(cmd, layer);
            cmd.SetTarget(layer).RemoveFromLayerTree().DeleteLayer();
        }
        cmd.Commit();
    }

    public static void MoveLayers(ImmutableArray<Entity> layers, Entity parent, int insertIndex)
    {
        var roots = OperationRoots(layers);
        var moving = roots.ToHashSet();
        var destination = parent.Get<LayerTreeNode>().Children.ToList();
        var anchor = destination.Skip(insertIndex).FirstOrDefault(e => !moving.Contains(e));
        var cmd = new CommandBuilder("Move Layers", parent.Document);
        foreach (var layer in roots)
        {
            destination.Remove(layer);
            int index = anchor.IsNull ? destination.Count : destination.IndexOf(anchor);
            destination.Insert(index, layer);
            AppendMoveLayer(cmd, layer, parent, index);
        }
        cmd.Commit();
    }

    private static void AppendMoveLayer(CommandBuilder cmd, Entity layer, Entity parent, int index)
    {
        var oldParent = layer.Get<LayerTreeNode>().ParentValue;
        int[] frames = oldParent != parent && layer.Tagged<CelTag>()
            ? [.. oldParent.Get<FolderLayerSetting>().Exposures.Where(p => p.Value == layer).Select(p => p.Key)]
            : [];
        if (frames.Length > 0)
            RemoveParentCelFolderExposures(cmd, layer);

        cmd.SetTarget(layer.Document).MoveLayer(layer, parent, index);
        if (frames.Length > 0 && parent.TryGet<FolderLayerSetting>() is { IsCelFolder: true })
            cmd.SetTarget(parent).SetObservableCollection(e => e.Get<FolderLayerSetting>().Exposures, exposures =>
            {
                foreach (int frame in frames)
                    if (!exposures.ContainsKey(frame)) exposures.Add(frame, layer);
            });
    }

    public static bool CanGroupLayers(ImmutableArray<Entity> layers)
    {
        var roots = OperationRoots(layers);
        return roots.Length == 1 || roots.Length > 1
            && !roots.Any(e => e.Tagged<CelTag>())
            && roots.Select(FindContainingCel).Distinct().Count() == 1;
    }

    public static void GroupLayers(ImmutableArray<Entity> layers)
    {
        var roots = OperationRoots(layers);
        var cmd = new CommandBuilder("Group Layers", layers[0].Document);
        Entity folder;
        if (roots.Length == 1)
        {
            folder = WrapSelfInFolder(cmd, roots[0]);
        }
        else
        {
            var parent = roots[0].Get<LayerTreeNode>().ParentValue;
            while (!roots.All(e => e.Get<LayerTreeNode>().EnumerateAncestors().Contains(parent)))
                parent = parent.Get<LayerTreeNode>().ParentValue;
            int index = roots.Max(e =>
            {
                var child = e;
                while (child.Get<LayerTreeNode>().ParentValue != parent)
                    child = child.Get<LayerTreeNode>().ParentValue;
                return child.Get<LayerTreeNode>().Index;
            });
            folder = layers[0].World.Create();
            cmd.SetTarget(folder).NewFolderLayer()
                .SetProperty(e => e.Get<CommonLayerSetting>().Name, layers[0].Get<CommonLayerSetting>().Name.Value)
                .AddToLayerTree(parent, index + 1);
            for (int i = 0; i < roots.Length; i++)
                cmd.SetTarget(layers[0].Document).MoveLayer(roots[i], folder, i);
        }
        cmd.SetTarget(folder).SetLayerSelection().Commit();
    }
}
