using System.Runtime.Serialization;
using Godot;
using R3;

namespace Ciallo.Data;

[DataContract, ToSerialize]
public class ShapeSetting
{
    [DataMember, ProjectField] public ReactiveProperty<Color> Color = new(Colors.Black);

    public ShapeSetting Clone() => new() { Color = { Value = Color.Value } };
}
