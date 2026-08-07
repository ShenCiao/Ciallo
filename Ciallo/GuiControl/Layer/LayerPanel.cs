using Ciallo.Data;
using Frent;
using Frent.Components;
using Godot;
using R3;

namespace Ciallo.GuiControl;

/// <summary>
/// Show layers, toggle LayerTree scenes' visibility according to current working document
/// </summary>
[SceneTree, Instantiable(init: "Initialize")]
public partial class LayerPanel : VBoxContainer, IInitable
{
    public void Init(Entity document)
    {
        var opacity = document.Get<SelectionManager>().WorkingLayer
            .Select(e => e.TryGet<CommonLayerSetting>()?.Opacity)
            .Flatten().AddTo(document);
        var layerMarkColor = document.Get<SelectionManager>().WorkingLayer
            .Select(e => e.TryGet<CommonLayerSetting>()?.MarkColor)
            .Flatten().AddTo(document);
        var blendMode = document.Get<SelectionManager>().WorkingLayer
            .Select(e => e.TryGet<CommonLayerSetting>()?.BlendMode)
            .Flatten().AddTo(document);
        LayerProperty.Opacity.BindNumber(opacity)
            .RegisterUndo(document.Get<CommandManager>());
        LayerProperty.LayerMark.BindColor(layerMarkColor)
            .RegisterUndo(document.Get<CommandManager>());
        LayerProperty.BlendMode.BindEnum(blendMode);
        document.Add(LayerTree);
        document.Add(LayerTree.RootContainer);
        LayerAction.Init(document);
    }
}
