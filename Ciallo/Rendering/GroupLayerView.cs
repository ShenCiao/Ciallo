using System;
using Ciallo.Data;
using Godot;
using R3;
using DataLayerBlendMode = Ciallo.Data.LayerBlendMode;

namespace Ciallo.Rendering;

/// <summary>
/// For layers using CanvasGroup, i.e. ShapeLayer and FolderLayer. CelLayer uses a custom CelFolderView instead.
/// </summary>
public partial class GroupLayerView : CanvasGroup
{
    // if true, this node can be replaced by a regular node2D
    public bool IsDefault =>
        SelfModulate.IsEqualApprox(Colors.White) &&
        GetLayerBlendMode() == LayerBlendModeEnum.Normal &&
        !IsClippingMask();

    public CompositeDisposable ObserveLayerSetting(CommonLayerSetting setting)
    {
        CompositeDisposable subs = new();
        setting.IsVisible.Subscribe(SetVisible).AddTo(subs);
        setting.Opacity.Subscribe(v =>
        {
            SelfModulate = SelfModulate with { A = v };
        }).AddTo(subs);
        setting.BlendMode.Subscribe(v => SetLayerBlendMode(ToGodotBlendMode(v))).AddTo(subs);
        setting.ClippingMask.Subscribe(SetClippingMask).AddTo(subs);
        return subs;
    }

    private static CanvasGroup.LayerBlendModeEnum ToGodotBlendMode(DataLayerBlendMode mode) => mode switch
    {
        DataLayerBlendMode.Normal => CanvasGroup.LayerBlendModeEnum.Normal,
        DataLayerBlendMode.Add => CanvasGroup.LayerBlendModeEnum.Add,
        DataLayerBlendMode.Multiply => CanvasGroup.LayerBlendModeEnum.Multiply,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
    };
}
