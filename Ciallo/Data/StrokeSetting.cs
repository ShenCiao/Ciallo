using System.Runtime.Serialization;
using Frent;
using R3;

namespace Ciallo.Data;

[DataContract, ToSerialize]
public class StrokeSetting
{
    [DataMember, ProjectField(StorageKind.Entity, EntityNullability.Nullable)]
    public ReactiveProperty<Entity> Brush = new(default);

    public StrokeSetting Clone()
    {
        return new StrokeSetting
        {
            Brush = { Value = Brush.Value },
        };
    }
}
