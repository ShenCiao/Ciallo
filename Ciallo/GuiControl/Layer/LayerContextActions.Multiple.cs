using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Ciallo.Command;
using Ciallo.Data;
using Frent;

namespace Ciallo.GuiControl;

internal static partial class LayerContextActions
{
    public static ImmutableArray<Entity> OperationRoots(ImmutableArray<Entity> layers)
        => layers.IsEmpty ? [] : layers[0].Document.Get<LayerTreeNode>().GetOperationRoots(layers);

    public static void DeleteLayers(ImmutableArray<Entity> layers)
    {
        if (layers.IsEmpty) return;
        var cmd = new CommandBuilder("Delete Layers", layers[0].Document);
        DeleteLayers(cmd, layers);
        cmd.Commit();
    }

    internal static void DeleteLayers(CommandBuilder cmd, ImmutableArray<Entity> layers)
    {
        var roots = OperationRoots(layers);
        var deleted = roots.ToHashSet();
        var document = layers[0].Document;
        var selected = document.Get<SelectionManager>().SelectedLayers.Value;
        ImmutableArray<Entity> survivors = [.. selected.Where(e => !LayerTreeNode.IsCoveredBy(e, deleted))];
        if (survivors.IsEmpty)
        {
            var focus = document.Get<LayerTreeNode>().GetNextFocusAfterDeletion(layers[0], deleted);
            survivors = focus.IsNull ? [] : [focus];
        }

        cmd.SetTarget(document).SelectLayers(layers: survivors);
        foreach (var layer in roots.Reverse())
        {
            RemoveParentCelFolderExposures(cmd, layer);
            cmd.SetTarget(layer).RemoveFromLayerTree().DeleteLayer();
        }
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
        var cmd = new CommandBuilder("Group Layers", layers[0].Document);
        var folder = GroupLayers(cmd, layers);
        cmd.SetTarget(folder).SelectLayers(recordCelSelectionPreference: true).Commit();
    }

    internal static Entity GroupLayers(CommandBuilder cmd, ImmutableArray<Entity> layers)
    {
        var roots = OperationRoots(layers);
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
        return folder;
    }
}
