using System.Runtime.Serialization;
using R3;
using Godot;

namespace Ciallo.Data;

/// <summary>
/// Common layer settings component for all layer types.
/// </summary>
[DataContract, ToSerialize]
public class CommonLayerSetting
{
    [DataMember, ProjectField] public ReactiveProperty<string> Name = new("");
    [DataMember, ProjectField] public ReactiveProperty<bool> IsVisible = new(true);
    [DataMember, ProjectField] public ReactiveProperty<float> Opacity = new(1.0f);
    [DataMember, ProjectField] public ReactiveProperty<Color?> MarkColor = new();
    [DataMember, ProjectField] public ReactiveProperty<bool> IsLocked = new(false);
    [DataMember, ProjectField] public ReactiveProperty<LayerBlendMode> BlendMode = new(LayerBlendMode.Normal);
    [DataMember, ProjectField] public ReactiveProperty<bool> ClippingMask = new(false);

    public void CopySettingFrom(CommonLayerSetting other)
    {
        Name.Value = other.Name.Value;
        IsVisible.Value = other.IsVisible.Value;
        Opacity.Value = other.Opacity.Value;
        MarkColor.Value = other.MarkColor.Value;
        IsLocked.Value = other.IsLocked.Value;
        BlendMode.Value = other.BlendMode.Value;
        ClippingMask.Value = other.ClippingMask.Value;
    }

    public CommonLayerSetting Clone()
    {
        return new CommonLayerSetting
        {
            Name = { Value = Name.Value },
            IsVisible = { Value = IsVisible.Value },
            Opacity = { Value = Opacity.Value },
            MarkColor = { Value = MarkColor.Value },
            IsLocked = { Value = IsLocked.Value },
            BlendMode = { Value = BlendMode.Value },
            ClippingMask = { Value = ClippingMask.Value },
        };
    }
}

/// <summary>
/// Blend modes supported by the layer renderer. The native enum has an additional
/// <c>Default</c> value for shader fallback; it is intentionally not user-facing here.
/// </summary>
public enum LayerBlendMode
{
    Normal,
    Add,
    Multiply,
}
