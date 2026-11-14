using System.Collections.Immutable;
using System.Runtime.Serialization;
using Godot;
using R3;

namespace Ciallo.Data;

[DataContract, ToSerialize]
public class ColorManager
{
    [DataMember, ProjectField] public ReactiveProperty<Color> CurrentColor = new(Colors.Black);
    [DataMember, ProjectField] public ReactiveProperty<ImmutableArray<Color>> Swatches = new([]);

    public void CopyFrom(ColorManager source)
    {
        CurrentColor.Value = source.CurrentColor.Value;
        Swatches.Value = source.Swatches.Value;
    }
}
