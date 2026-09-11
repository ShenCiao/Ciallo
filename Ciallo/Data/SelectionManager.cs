using System.Collections.Immutable;
using System.Linq;
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
    public ReactiveProperty<ImmutableArray<Entity>> SelectedLayers = new([]);

    public ReadOnlyReactiveProperty<Entity> PrimaryLayer { get; }
    public ReadOnlyReactiveProperty<Entity> WorkingCelFolder; // Null if the primary layer is not under any cel folder.

    [DataMember, ProjectField(StorageKind.Entity, EntityNullability.Nullable)]
    public ReactiveProperty<Entity> WorkingStrokeBrush = new(Entity.Null);

    [DataMember, ProjectField(StorageKind.Entity, EntityNullability.Nullable)]
    public ReactiveProperty<Entity> WorkingVectorFillBrush = new(Entity.Null);

    public ObservableList<Entity> SelectedShapes = [];

    public SelectionManager()
    {
        PrimaryLayer = SelectedLayers.Select(layers => layers.IsEmpty ? Entity.Null : layers[0])
            .ToReadOnlyReactiveProperty();
    }

    public void InitWorkingCelFolder(LayerTreeNode root)
    {
        var layerTreeChanged = root.ObserveMutation().DebounceFrame(1).ObserveOn(GodotFrameProvider.BeforeProcess);
        WorkingCelFolder = layerTreeChanged.CombineLatest(PrimaryLayer, (_, layerE) => layerE)
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
    /// Resolves the ordered template for the exposed cel. A default array means there is no
    /// cel-folder context. Blank or wholly missing selections retain the folder as navigation
    /// context, with no drawable layer selected. Missing names remain in the template.
    /// </summary>
    public ImmutableArray<Entity> ResolveLayersForTimelineFrameSelection(int frame)
    {
        var celFolder = WorkingCelFolder.CurrentValue;
        if (celFolder.IsNull) return default;
        var exposures = celFolder.Get<FolderLayerSetting>().Exposures;
        int floor = exposures.FloorIndex(frame);
        return ResolveLayersForCelSelection(celFolder,
            floor < 0 ? Entity.Null : exposures.GetValueAtIndex(floor));
    }

    public static ImmutableArray<Entity> ResolveLayersForCelSelection(Entity celFolder, Entity cel)
    {
        if (!cel.IsNull && !cel.IsCelFolder && !cel.Has<FolderLayerSetting>())
            return [cel];
        var names = celFolder.Get<FolderLayerSetting>().PreferredNamesForCelSelection.Value;
        var layers = CelLayerSelection.Resolve(cel, names);
        return layers.IsEmpty ? [celFolder] : layers;
    }

    public bool NeedsTimelineSelectionCommit(ImmutableArray<Entity> layers) =>
        !layers.IsDefault && !SelectedLayers.Value.SequenceEqual(layers);

    /// <summary>
    /// Returns the frame index to switch to after switching primary layer.
    /// <list type="bullet">
    /// <item>If <paramref name="selectedPrimaryLayer"/> is null, the document, outside any cel folder,
    ///   or is itself a cel folder root, returns the current frame (no switch).</item>
    /// <item>Otherwise finds the direct cel under the nearest cel folder ancestor and searches all
    ///   exposure ranges that show that cel.</item>
    /// <item>If the current frame already lies inside one of those ranges, returns that range's start frame.</item>
    /// <item>Otherwise returns the nearest matching range start frame. Ties prefer the earlier frame.</item>
    /// <item>If the layer is never exposed, returns the current frame.</item>
    /// </list>
    /// </summary>
    public int ComputeFrameForPrimaryLayerSelection(Entity selectedPrimaryLayer)
    {
        int currentFrame = CurrentFrame.Value;
        if (selectedPrimaryLayer.IsNull || selectedPrimaryLayer.IsDocument || !selectedPrimaryLayer.IsAlive)
            return currentFrame;

        if (selectedPrimaryLayer.TryGet<FolderLayerSetting>()?.IsCelFolder == true)
            return currentFrame;

        Entity celFolder = Entity.Null;
        Entity targetCel = Entity.Null;
        var cursor = selectedPrimaryLayer;

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
