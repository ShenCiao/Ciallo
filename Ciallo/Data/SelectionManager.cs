using System.Collections.Immutable;
using System.Runtime.Serialization;
using Frent;
using ObservableCollections;
using R3;
using Godot;

namespace Ciallo.Data;

[DataContract, ToSerialize]
public class SelectionManager
{
    /// <summary>Current playhead position.</summary>
    [DataMember, ProjectField] public ReactiveProperty<int> CurrentFrame = new(1);

    // Ordered selection. The first layer is the drawing target; the others join layer operations.
    [DataMember, ProjectField(StorageKind.Entity, EntityNullability.Required)]
    public ReactiveProperty<ImmutableArray<Entity>> WorkingLayers = new([]);

    public ReadOnlyReactiveProperty<Entity> WorkingLayer { get; }
    public ReadOnlyReactiveProperty<Entity> WorkingCelFolder; // Null if the working layer is not under any cel folder.

    [DataMember, ProjectField(StorageKind.Entity, EntityNullability.Nullable)]
    public ReactiveProperty<Entity> WorkingStrokeBrush = new(Entity.Null);

    [DataMember, ProjectField(StorageKind.Entity, EntityNullability.Nullable)]
    public ReactiveProperty<Entity> WorkingVectorFillBrush = new(Entity.Null);

    public ObservableList<Entity> SelectedShapes = [];

    public SelectionManager()
    {
        WorkingLayer = WorkingLayers.Select(layers => layers.IsEmpty ? Entity.Null : layers[0])
            .ToReadOnlyReactiveProperty();
    }

    public void InitWorkingCelFolder(LayerTreeNode root)
    {
        var layerTreeChanged = root.ObserveMutation().DebounceFrame(1).ObserveOn(GodotFrameProvider.BeforeProcess);
        WorkingCelFolder = layerTreeChanged.CombineLatest(WorkingLayer, (_, layerE) => layerE)
            .Select(layerE =>
            {
                if (layerE.IsNull || layerE.IsDocument)
                    return Entity.Null;
                if (layerE.TryGet<FolderLayerSetting>()?.IsCelFolder == true)
                {
                    return layerE;
                }
                if (layerE.Tagged<CelTag>())
                    return layerE.Get<LayerTreeNode>().ParentValue;

                var ancestors = layerE.Get<LayerTreeNode>().EnumerateAncestors();
                foreach (Entity e in ancestors)
                {
                    if (e.Tagged<CelTag>()) return e.Get<LayerTreeNode>().ParentValue;
                }

                return Entity.Null;
            }).ToReadOnlyReactiveProperty();
    }

    /// <summary>
    /// Resolves the layer that should be selected for a timeline frame, using the
    /// working cel folder's selected cel and its preferred cel child name.
    /// <list type="bullet">
    /// <item>Returns <see cref="Entity.Null"/> when there is no working cel folder, i.e. the
    ///   current working layer is not under any cel folder. The caller should keep the working
    ///   layer untouched (scrubbing must not disturb plain-layer editing).</item>
    /// <item>Returns the document entity when a working cel folder exists but the frame resolves
    ///   to no cel child (frame before the first cel, dead cel, or no direct child matching the
    ///   preferred name). The caller commits this so the working layer is cleared.</item>
    /// <item>Returns the working cel folder itself for a Blank exposure.</item>
    /// <item>Otherwise returns the matching cel child to switch to.</item>
    /// </list>
    /// </summary>
    public Entity ResolveWorkingLayerForTimelineFrameSelection(int frame)
    {
        var celFolder = WorkingCelFolder.CurrentValue;
        // Not in a cel-folder context: keep the current working layer (no change).
        if (celFolder.IsNull) return Entity.Null;

        var folderSetting = celFolder.Get<FolderLayerSetting>();
        var exposures = folderSetting.Exposures;
        if (exposures == null) return Entity.Null;

        // In a cel-folder context from here on: a miss means "clear", signalled by the document entity.
        int floor = exposures.FloorIndex(frame);
        if (floor < 0)
            return celFolder.Document;

        var exposedCel = exposures.GetValueAtIndex(floor);
        if (exposedCel.IsNull || !exposedCel.IsAlive || !exposedCel.Has<LayerTreeNode>())
            return celFolder.Document;
        if (exposedCel.IsCelFolder)
            return celFolder;

        if (!exposedCel.Has<FolderLayerSetting>())
            return exposedCel;

        var child = exposedCel.Get<LayerTreeNode>().GetLayerChildByName(folderSetting.PreferredNameForCelSelection.Value);
        return child.IsNull ? celFolder.Document : child;
    }

    /// <summary>
    /// Returns the entity to switch <see cref="WorkingLayer"/> to after clicking a cel button,
    /// using the clicked cel's direct child matching the folder's preferred cel child name.
    /// <list type="bullet">
    /// <item>Returns <see cref="Entity.Null"/> when the arguments are invalid, or the resolved
    ///   child is already the working layer (nothing to do).</item>
    /// <item>Returns the cel folder itself when the clicked button is a Blank exposure.</item>
    /// <item>Returns the document entity when the clicked cel has no direct child matching the
    ///   preferred name, so the caller clears the working layer.</item>
    /// <item>Otherwise returns the matching cel child.</item>
    /// </list>
    /// </summary>
    public Entity ComputeWorkingLayerForCelButtonSelection(Entity celFolder, Entity clickedCel)
    {
        if (celFolder.IsNull || !celFolder.IsAlive)
            return Entity.Null;
        if (clickedCel.IsNull || !clickedCel.IsAlive || !clickedCel.Has<LayerTreeNode>())
            return Entity.Null;

        var folderSetting = celFolder.TryGet<FolderLayerSetting>();
        if (folderSetting?.IsCelFolder != true)
            return Entity.Null;

        Entity result;
        if (clickedCel.IsCelFolder)
        {
            result = celFolder;
        }
        else if (!clickedCel.Tagged<CelTag>())
        {
            return Entity.Null;
        }
        else if (clickedCel.Has<FolderLayerSetting>())
        {
            // No matching child: clear the working layer, signalled by the document entity.
            var child = clickedCel.Get<LayerTreeNode>().GetLayerChildByName(folderSetting.PreferredNameForCelSelection.Value);
            result = child.IsNull ? celFolder.Document : child;
        }
        else
        {
            result = clickedCel;
        }

        // Already the working layer (including already-cleared): nothing to do.
        return result == WorkingLayer.CurrentValue && WorkingLayers.Value.Length == 1 ? Entity.Null : result;
    }

    /// <summary>
    /// Returns the frame index to switch to after switching working layer.
    /// <list type="bullet">
    /// <item>If <paramref name="selectedWorkingLayer"/> is null, the document, outside any cel folder,
    ///   or is itself a cel folder root, returns the current frame (no switch).</item>
    /// <item>Otherwise finds the direct cel under the nearest cel folder ancestor and searches all
    ///   exposure ranges that show that cel.</item>
    /// <item>If the current frame already lies inside one of those ranges, returns that range's start frame.</item>
    /// <item>Otherwise returns the nearest matching range start frame. Ties prefer the earlier frame.</item>
    /// <item>If the layer is never exposed, returns the current frame.</item>
    /// </list>
    /// </summary>
    public int ComputeFrameForWorkingLayerSelection(Entity selectedWorkingLayer)
    {
        int currentFrame = CurrentFrame.Value;
        if (selectedWorkingLayer.IsNull || selectedWorkingLayer.IsDocument || !selectedWorkingLayer.IsAlive)
            return currentFrame;

        if (selectedWorkingLayer.TryGet<FolderLayerSetting>()?.IsCelFolder == true)
            return currentFrame;

        Entity celFolder = Entity.Null;
        Entity targetCel = Entity.Null;
        var cursor = selectedWorkingLayer;

        while (!cursor.IsNull && !cursor.IsDocument)
        {
            if (cursor.Tagged<CelTag>())
            {
                celFolder = cursor.Get<LayerTreeNode>().ParentValue;
                targetCel = cursor;
                break;
            }

            cursor = cursor.Get<LayerTreeNode>().ParentValue;
        }

        if (celFolder.IsNull || targetCel.IsNull) return currentFrame;

        var exposures = celFolder.Get<FolderLayerSetting>().Exposures;
        if (exposures == null || exposures.Count == 0) return currentFrame;

        int bestFrame = currentFrame;
        int bestDistance = int.MaxValue;

        for (int i = 0; i < exposures.Count; i++)
        {
            if (exposures.GetValueAtIndex(i) != targetCel) continue;

            int start = exposures.GetKeyAtIndex(i);
            int endExclusive = i + 1 < exposures.Count ? exposures.GetKeyAtIndex(i + 1) : int.MaxValue;

            if (currentFrame >= start && currentFrame < endExclusive)
                return start;

            int candidate = start;
            int distance = Mathf.Abs(candidate - currentFrame);
            if (distance < bestDistance || (distance == bestDistance && candidate < bestFrame))
            {
                bestFrame = candidate;
                bestDistance = distance;
            }
        }

        return bestDistance == int.MaxValue ? currentFrame : bestFrame;
    }
}
