using System;
using Ciallo.Data;
using Ciallo.Widget;
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
        var selection = document.Get<SelectionManager>();
        LayerSelectionActions.ObservePrimary(selection, s => s.Opacity)
            .Subscribe(v => LayerProperty.Opacity.SetValueNoSignal(v)).AddTo(document);
        LayerProperty.Opacity.SignalAsObservable<double, double>(SpinSlider.SignalName.ValueChanged)
            .Subscribe(v => LayerSelectionActions.SetProperty("Layer Opacity", selection.WorkingLayers.Value,
                s => s.Opacity, (float)v.Item2, sequence: true)).AddTo(document);
        LayerSelectionActions.ObservePrimary(selection, s => s.MarkColor)
            .Subscribe(LayerProperty.LayerMark.SetColorOrNullNoSignal).AddTo(document);
        LayerProperty.LayerMark.ColorOrNullChanged.Subscribe(v => LayerSelectionActions.SetProperty(
            "Layer Mark Color", selection.WorkingLayers.Value, s => s.MarkColor, v, sequence: true)).AddTo(document);

        var modes = Enum.GetValues<LayerBlendMode>();
        LayerProperty.BlendMode.Clear();
        foreach (var mode in modes) LayerProperty.BlendMode.AddItem(mode.ToString().Tr());
        LayerProperty.BlendMode.AllowReselect = true;
        LayerSelectionActions.ObservePrimary(selection, s => s.BlendMode)
            .Subscribe(v => LayerProperty.BlendMode.Select(Array.IndexOf(modes, v))).AddTo(document);
        LayerProperty.BlendMode.OnItemSelectedAsObservable().Subscribe(index => LayerSelectionActions.SetProperty(
            "Layer Blend Mode", selection.WorkingLayers.Value, s => s.BlendMode, modes[(int)index])).AddTo(document);
        document.Add(LayerTree);
        document.Add(LayerTree.RootContainer);
        LayerAction.Init(document);
    }
}
