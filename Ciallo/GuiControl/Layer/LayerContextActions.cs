using System.Collections.Generic;
using System.Linq;
using Ciallo.Command;
using Ciallo.Data;
using Frent;

namespace Ciallo.GuiControl;

internal static class LayerContextActions
{
    private static int _plainShapeLayerId = 1;

    public static void NewShapeLayer(Entity targetLayer)
    {
        if (TryAddShapeLayerInCelContext(targetLayer))
            return;

        var document = targetLayer.Document;
        var (parentE, index) = GetNewLayerInsertPosition(targetLayer);
        new CommandBuilder("New Shape Layer", document.World.Create())
            .NewShapeLayer()
            .SetProperty(e => e.Get<CommonLayerSetting>().Name, $"{"Shape layer".Tr()} {_plainShapeLayerId++}")
            .AddToLayerTree(parentE, index)
            .SetWorkingLayer()
            .Commit();
    }

    public static void NewFolderLayer(Entity targetLayer)
    {
        var document = targetLayer.Document;
        var (parentE, index) = GetNewLayerInsertPosition(targetLayer);
        new CommandBuilder("New Folder Layer", document.World.Create())
            .NewFolderLayer()
            .AddToLayerTree(parentE, index)
            .SetWorkingLayer()
            .Commit();
    }

    public static void NewCelFolderLayer(Entity targetLayer)
    {
        var document = targetLayer.Document;
        var (parentE, index) = GetNewCelFolderInsertPosition(targetLayer);
        new CommandBuilder("New Cel Folder", document.World.Create())
            .NewCelFolder()
            .AddToLayerTree(parentE, index)
            .SetWorkingLayer()
            .Commit();
    }

    public static void DeleteLayer(Entity targetLayer)
    {
        var nextLayer = GetNextFocusLayerAfterDeletion(targetLayer);
        var cmd = new CommandBuilder("Delete Layer", nextLayer)
            .SetWorkingLayer();

        RemoveParentCelFolderExposures(cmd, targetLayer);

        cmd.SetTarget(targetLayer)
            .RemoveFromLayerTree()
            .DeleteLayer()
            .Commit();
    }

    public static void UngroupFolder(Entity targetFolder)
    {
        if (!targetFolder.Has<FolderLayerSetting>()) return;

        var folderNode = targetFolder.Get<LayerTreeNode>();
        var parentE = folderNode.ParentValue;
        if (parentE.IsNull) return;

        var children = folderNode.Children.ToArray();
        var nextLayer = GetNextFocusLayerAfterDeletion(targetFolder);
        int insertIndex = folderNode.Index;

        var cmd = new CommandBuilder("Ungroup Folder", nextLayer)
            .SetWorkingLayer();

        RemoveParentCelFolderExposures(cmd, targetFolder);

        for (int i = 0; i < children.Length; i++)
        {
            cmd.SetTarget(targetFolder.Document)
                .MoveLayer(children[i], parentE, insertIndex + i);
        }

        cmd.SetTarget(targetFolder)
            .RemoveFromLayerTree()
            .DeleteLayer()
            .Commit();
    }

    public static void RenameCelsByExposure(Entity celFolder)
    {
        var exposures = celFolder.Get<FolderLayerSetting>().Exposures;
        var renamedCels = new HashSet<Entity>();
        var cmd = new CommandBuilder("Rename Cels by Exposure", celFolder);
        int name = 1;

        foreach (var cel in exposures.Values)
        {
            if (cel.IsCelFolder || !renamedCels.Add(cel))
                continue;

            cmd.SetTarget(cel)
                .SetProperty(e => e.Get<CommonLayerSetting>().Name, name.ToString());
            name++;
        }

        cmd.Commit();
    }

    public static void WrapChildrenInFolders(Entity targetFolder)
    {
        var children = targetFolder.Get<LayerTreeNode>().Children.ToArray();
        if (children.Length == 0) return;

        var document = targetFolder.Document;
        var cmd = new CommandBuilder("Wrap Children in Folders", document);
        bool targetIsCelFolder = targetFolder.Get<FolderLayerSetting>().IsCelFolder;

        foreach (var child in children)
        {
            var wrapper = document.World.Create();
            var childName = child.Get<CommonLayerSetting>().Name.Value;
            var childIndex = child.Get<LayerTreeNode>().Index;

            cmd.SetTarget(wrapper)
                .NewFolderLayer()
                .SetProperty(e => e.Get<CommonLayerSetting>().Name, childName)
                .AddToLayerTree(targetFolder, childIndex);

            if (targetIsCelFolder)
            {
                cmd.SetTarget(targetFolder)
                    .SetObservableCollection(
                        e => e.Get<FolderLayerSetting>().Exposures,
                        exposures =>
                        {
                            foreach (var (frame, cel) in exposures.ToArray())
                            {
                                if (cel == child)
                                    exposures[frame] = wrapper;
                            }
                        });
            }

            cmd.SetTarget(document)
                .MoveLayer(child, wrapper, 0);
        }

        cmd.Commit();
    }

    public static void WrapSelfInFolder(Entity targetLayer)
    {
        if (targetLayer.IsNull || targetLayer.IsDocument) return;

        var node = targetLayer.Get<LayerTreeNode>();
        var parentE = node.ParentValue;
        if (parentE.IsNull) return;

        var document = targetLayer.Document;
        var name = targetLayer.Get<CommonLayerSetting>().Name.Value;
        var index = node.Index;
        var wrapper = document.World.Create();

        var cmd = new CommandBuilder("Wrap Self in Folder", document)
            .SetTarget(wrapper)
            .NewFolderLayer()
            .SetProperty(e => e.Get<CommonLayerSetting>().Name, name)
            .AddToLayerTree(parentE, index);

        // If wrapped inside a cel folder, hand the wrapper the cel's exposure slots.
        if (parentE.TryGet<FolderLayerSetting>() is { IsCelFolder: true })
        {
            cmd.SetTarget(parentE)
                .SetObservableCollection(
                    e => e.Get<FolderLayerSetting>().Exposures,
                    exposures =>
                    {
                        foreach (var (frame, cel) in exposures.ToArray())
                        {
                            if (cel == targetLayer)
                                exposures[frame] = wrapper;
                        }
                    });
        }

        cmd.SetTarget(document)
            .MoveLayer(targetLayer, wrapper, 0);

        cmd.SetTarget(targetLayer)
            .SetWorkingLayer()
            .Commit();
    }

    /// <summary>
    /// Cel-aware "add shape layer". Distinguishes batch edits from single-cel edits:
    /// <list type="bullet">
    ///   <item><b>Target is a cel folder</b> (batch): add a shape layer at the visual top of every
    ///     folder-cel, all sharing one name so they collapse to a single archetype row. Non-folder cels
    ///     are skipped. If no folder-cel exists, fall back to the new-cel flow (a fresh folder cel whose
    ///     child is the shape layer).</item>
    ///   <item><b>Target lands inside one cel</b> (single): add the shape layer to that cel only, named
    ///     with a leading '_' so it stays out of the archetype system — unless the cel folder holds just
    ///     one cel, in which case this edit IS the archetype, so no '_' prefix.</item>
    /// </list>
    /// Returns false when there is no cel context, letting the caller do an ordinary insert.
    /// </summary>
    private static bool TryAddShapeLayerInCelContext(Entity targetLayer)
    {
        if (targetLayer.IsNull || targetLayer.IsDocument)
            return false;

        // Target is the cel folder itself → batch over all its cels.
        if (targetLayer.TryGet<FolderLayerSetting>() is { IsCelFolder: true })
        {
            AddShapeLayerToAllCels(targetLayer);
            return true;
        }

        // Otherwise find the single cel the new layer would land in (target is the cel, or inside it).
        var cel = FindContainingCel(targetLayer);
        if (cel.IsNull || !cel.Has<FolderLayerSetting>())
            return false; // non-folder cel can't hold children → ordinary insert handles it

        AddShapeLayerToSingleCel(cel);
        return true;
    }

    /// <summary>Walks up from <paramref name="layer"/> to the nearest cel (a CelTag-tagged ancestor or self).</summary>
    private static Entity FindContainingCel(Entity layer)
    {
        var cursor = layer;
        while (!cursor.IsNull && !cursor.IsDocument && cursor.Has<LayerTreeNode>())
        {
            if (cursor.Tagged<CelTag>())
                return cursor;
            cursor = cursor.Get<LayerTreeNode>().ParentValue;
        }
        return Entity.Null;
    }

    private static void AddShapeLayerToAllCels(Entity celFolder)
    {
        var folderCels = celFolder.Get<LayerTreeNode>().Children
            .Where(c => c.IsAlive && c.Tagged<CelTag>() && c.Has<FolderLayerSetting>())
            .ToArray();

        // No folder-cel to add into → bootstrap a brand-new cel like the New Animation Cel button.
        if (folderCels.Length == 0)
        {
            var document = celFolder.Document;
            int currentFrame = document.Get<SelectionManager>().CurrentFrame.Value;
            var (frame, name) = TimelineAction.GetNewAnimationCelFrameName(celFolder, currentFrame);
            TimelineAction.NewCelFromArchetype(celFolder, frame, name);
            return;
        }

        // One shared name across every cel, so the new layers collapse into a single archetype row.
        // Reuse the plain-path counter (one bump per batch) — laziest way to keep repeated batches distinct.
        string sharedName = $"{"Shape layer".Tr()} {_plainShapeLayerId++}";

        var cmd = new CommandBuilder("Add Shape Layer to All Cels", celFolder.Document);
        Entity workingLayerE = Entity.Null;
        var exposedCel = celFolder.Get<FolderLayerSetting>().CurrentExposedCel.CurrentValue;

        foreach (var cel in folderCels)
        {
            var shapeE = celFolder.World.Create();
            cmd.SetTarget(shapeE)
                .NewShapeLayer()
                .SetProperty(e => e.Get<CommonLayerSetting>().Name, sharedName)
                .AddToLayerTree(cel); // -1 default = last child = visual top

            if (cel == exposedCel)
                workingLayerE = shapeE;
        }

        // Land the working layer on the new layer in the currently-exposed cel, so the user can draw at once.
        // Record the preference so cel navigation follows the just-added shared layer, not the old archetype row.
        if (!workingLayerE.IsNull)
            cmd.SetTarget(workingLayerE).SetWorkingLayer(recordCelSelectionPreference: true);

        cmd.Commit();
    }

    private static void AddShapeLayerToSingleCel(Entity cel)
    {
        var celFolder = cel.Get<LayerTreeNode>().ParentValue;
        int celCount = celFolder.Get<LayerTreeNode>().Children.Count(c => c.IsAlive && c.Tagged<CelTag>());

        // Single-cel edit gets a '_' prefix to stay out of the archetype — except when this is the
        // folder's only cel, where the edit defines the archetype and must NOT be hidden from it.
        bool isBatch = celCount <= 1;
        string baseName = $"{"Shape layer".Tr()} {_plainShapeLayerId++}";
        string name = isBatch ? baseName : "_" + baseName;

        var shapeE = cel.World.Create();
        new CommandBuilder("Add Shape Layer", cel.Document)
            .SetTarget(shapeE)
            .NewShapeLayer()
            .SetProperty(e => e.Get<CommonLayerSetting>().Name, name)
            .AddToLayerTree(cel)
            .SetWorkingLayer(recordCelSelectionPreference: true)
            .Commit();
    }

    private static (Entity parentE, int index) GetNewLayerInsertPosition(Entity targetLayer)
    {
        if (targetLayer.IsNull || targetLayer.IsDocument)
            return (AppDocumentManager.WorkingDocument.Value, -1);

        if (targetLayer.Has<FolderLayerSetting>())
            return (targetLayer, -1);

        var layerNode = targetLayer.Get<LayerTreeNode>();
        return (layerNode.ParentValue, layerNode.Index + 1);
    }

    private static (Entity parentE, int index) GetNewCelFolderInsertPosition(Entity targetLayer)
    {
        if (targetLayer.IsNull || targetLayer.IsDocument)
            return (AppDocumentManager.WorkingDocument.Value, -1);

        if (targetLayer.TryGet<FolderLayerSetting>() is { IsCelFolder: false })
            return (targetLayer, -1);

        var cursor = targetLayer;
        Entity nearestCelFolder = Entity.Null;

        while (!cursor.IsDocument)
        {
            if (cursor.Has<FolderLayerSetting>())
            {
                if (cursor.Get<FolderLayerSetting>().IsCelFolder)
                {
                    nearestCelFolder = cursor;
                    break;
                }
            }

            cursor = cursor.Get<LayerTreeNode>().ParentValue;
        }

        if (!nearestCelFolder.IsNull)
        {
            var celFolderNode = nearestCelFolder.Get<LayerTreeNode>();
            return (celFolderNode.ParentValue, celFolderNode.Index + 1);
        }

        var layerNode = targetLayer.Get<LayerTreeNode>();
        return (layerNode.ParentValue, layerNode.Index + 1);
    }

    private static Entity GetNextFocusLayerAfterDeletion(Entity targetLayer)
    {
        var document = targetLayer.Document;
        var root = document.Get<LayerTreeNode>();
        var targetPath = root.FindPathTo(targetLayer);
        var nextLayerPath = root.GetNextFocusPathAfterDeletion(targetPath);
        return nextLayerPath.IsEmpty ? document : root.GetDescendant(nextLayerPath);
    }

    private static void RemoveParentCelFolderExposures(CommandBuilder cmd, Entity targetLayer)
    {
        if (!targetLayer.Tagged<CelTag>())
            return;

        var parentE = targetLayer.Get<LayerTreeNode>().ParentValue;
        cmd.SetTarget(parentE)
            .SetObservableCollection(
                e => e.Get<FolderLayerSetting>().Exposures,
                exposures =>
                {
                    foreach (var (frame, celE) in exposures.ToArray())
                    {
                        if (celE == targetLayer)
                            exposures.Remove(frame);
                    }
                });
    }
}
