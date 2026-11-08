using System.Runtime.Serialization;
using Frent;
using ObservableCollections;
using R3;

namespace Ciallo.Data;

[DataContract, ToSerialize]
public class BrushManager
{
    public const float DefaultStrokeRadius = 1.5f;

    [DataMember, ProjectField]
    public ReactiveProperty<float> CurrentStrokeRadius = new(DefaultStrokeRadius);

    [DataMember, ProjectField(StorageKind.Entity, EntityNullability.Required)]
    public ObservableList<Entity> StrokeBrushes = [];
    [DataMember, ProjectField(StorageKind.Entity, EntityNullability.Required)]
    public ObservableList<Entity> VectorFillBrushes = [];
}
