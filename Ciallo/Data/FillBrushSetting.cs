using System;
using System.Runtime.Serialization;
using Godot;
using R3;

namespace Ciallo.Data;

[DataContract, ToSerialize]
public class FillBrushSetting
{
    [DataMember, ProjectField]
    public ReactiveProperty<string> Name = new("");

    [DataMember, ProjectField(StorageKind.Blob)]
    public ReactiveProperty<ImageTexture> MarkerTexture = new(null);
    [DataMember, ProjectField]
    public ReactiveProperty<Color> MarkerColor = new(Colors.Black);

    [DataMember, ProjectField]
    public ReactiveProperty<Color> FillColor = new(Colors.Black);
    [DataMember, ProjectField]
    public ReactiveProperty<FillBrushFlags> ActiveBrushFlags = new();

    public Color ResolveColor(Color shapeColor) => new(
        ActiveBrushFlags.Value.HasFlag(FillBrushFlags.UseShapeRgb) ? shapeColor : FillColor.Value)
    {
        A = ActiveBrushFlags.Value.HasFlag(FillBrushFlags.UseShapeAlpha) ? shapeColor.A : FillColor.Value.A,
    };

    public FillBrushSetting Clone()
    {
        return new FillBrushSetting
        {
            Name = { Value = Name.Value },
            MarkerTexture = { Value = MarkerTexture.Value },
            MarkerColor = { Value = MarkerColor.Value },
            FillColor = { Value = FillColor.Value },
            ActiveBrushFlags = { Value = ActiveBrushFlags.Value },
        };
    }
}

[Flags]
public enum FillBrushFlags
{
    UseShapeRgb = 1 << 0,
    UseShapeAlpha = 1 << 1,
}
