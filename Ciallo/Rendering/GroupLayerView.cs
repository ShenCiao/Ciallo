using System;
using Ciallo.Data;
using Godot;
using R3;
using DataLayerBlendMode = Ciallo.Data.LayerBlendMode;

namespace Ciallo.Rendering;

/// <summary>
/// For composite-capable Ciallo layers, i.e. ShapeLayer and FolderLayer. CelLayer uses a custom CelFolderView instead.
/// </summary>
public partial class GroupLayerView : Layer2D
{
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

    private static Layer2D.LayerBlendModeEnum ToGodotBlendMode(DataLayerBlendMode mode) => mode switch
    {
        // Ciallo's ordinary Normal mode is the default selection. This keeps
        // an unchanged layer eligible for direct Node2D rendering.
        DataLayerBlendMode.Normal => Layer2D.LayerBlendModeEnum.Default,
        DataLayerBlendMode.Add => Layer2D.LayerBlendModeEnum.Add,
        DataLayerBlendMode.Multiply => Layer2D.LayerBlendModeEnum.Multiply,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
    };
}
