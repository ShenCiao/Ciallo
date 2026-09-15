using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.Serialization;
using Frent;
using ObservableCollections;
using R3;

namespace Ciallo.Data;

[DataContract, ToSerialize]
public class FolderLayerSetting
{
    [DataMember, ProjectField] public ReactiveProperty<bool> IsExpanded = new(true);
    /// <summary>
    /// When true this folder acts as a frame-by-frame (celluloid) animation track.
    /// Its children are treated as cels going to be placed on trace.
    /// </summary>
    /// <remarks>
    /// By design, cel folders cannot be nested each other, but can freely contain or be contained by regular folders
    /// which means at any path from root(document entity) to leaf, there must be at most one cel folder.
    ///
    /// Cel is pronounced in JP style "seru" (セ ル) or in its full name "celluloid".
    /// </remarks>
    public bool IsCelFolder
    {
        get => Exposures != null;
        set => Exposures = value ? (Exposures ?? []) : null;
    }

    public FolderLayerSetting Clone() =>
        new()
        {
            IsExpanded = { Value = IsExpanded.Value },
            Exposures = Exposures is null ? null : [.. Exposures],
            PreferredNamesForCelSelection = { Value = PreferredNamesForCelSelection.Value },
        };

    #region Cel Folder

    /// <summary>
    /// Cel exposure table. One entry is one exposure: the key is its exposure key (starting frame),
    /// the value is the cel it exposes until the next key. A span (duration) is never stored — it is
    /// implied by the distance to the next key.
    /// A CelFolder value represents an explicit Blank exposure; authored Blank values use this
    /// setting's owning CelFolder entity.
    /// </summary>
    [DataMember, ProjectField(StorageKind.Entity, EntityNullability.Required)]
    public ObservableSortedList<int, Entity> Exposures = null;

    public ReadOnlyReactiveProperty<Entity> CurrentExposedCel { get; private set; }
    /// <summary>
    /// Onion skin cels keyed by exposure-index offset.
    /// </summary>
    public ReadOnlyReactiveProperty<SortedList<int, Entity>> CurrentOnionSkinCels { get; private set; }

    public void InitCurrent(ReactiveProperty<int> currentFrame, Observable<ImmutableArray<int>> onionSkinOffsets)
    {
        CurrentExposedCel = Exposures.ObserveChanged().PrependDefault()
            .CombineLatest(currentFrame, (_, frame) => Exposures.FloorIndex(frame))
            .Select(idx => idx >= 0 ? Exposures.GetValueAtIndex(idx) : Entity.Null)
            .ToReadOnlyReactiveProperty();

        CurrentOnionSkinCels = Exposures.ObserveChanged().PrependDefault()
            .CombineLatest(onionSkinOffsets, currentFrame,
                (_, offsets, frame) =>
                {
                    var cels = new SortedList<int, Entity>();
                    int currentExposureIndex = Exposures.FloorIndex(frame);
                    if (currentExposureIndex < 0)
                        return cels;

                    foreach (var offset in offsets)
                    {
                        int targetExposureIndex = currentExposureIndex + offset;
                        if (targetExposureIndex < 0 || targetExposureIndex >= Exposures.Count)
                            continue;

                        cels[offset] = Exposures.GetValueAtIndex(targetExposureIndex);
                    }

                    return cels;
                }
            ).ToReadOnlyReactiveProperty();
    }

    // Name indexed children set. Used for batch modification of cel children layers.
    public readonly ObservableDictionary<string, ObservableHashSet<Entity>> CelChildrenByName = new();

    /// <summary>
    /// Ordered cel-child selection template. Navigation selects matching direct children in this
    /// order, temporarily promoting the first available name without removing missing names.
    /// Explicit selection edits and archetype operations update the template as part of undo history.
    /// </summary>
    [DataMember, ProjectField]
    public ReactiveProperty<ImmutableArray<string>> PreferredNamesForCelSelection = new([]);

    #endregion
}
