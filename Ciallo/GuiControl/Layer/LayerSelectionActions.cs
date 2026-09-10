using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Ciallo.Command;
using Ciallo.Data;
using Frent;
using Godot;
using R3;

namespace Ciallo.GuiControl;

internal static class LayerSelectionActions
{
    private static readonly Texture2D BrushIcon = GD.Load<Texture2D>("res://Icon/FixedSize/brush-icon-sized.svg");
    private static readonly Texture2D CheckIcon = GD.Load<Texture2D>("res://Icon/FixedSize/check-icon-sized.svg");

    public static void SelectOnly(Entity layer)
    {
        var selection = layer.Document.Get<SelectionManager>();
        int frame = selection.ComputeFrameForWorkingLayerSelection(layer);
        if (selection.WorkingLayers.Value.Length == 1 && selection.WorkingLayer.CurrentValue == layer
            && frame == selection.CurrentFrame.Value)
            return;

        new CommandBuilder("Select Layer", layer)
            .SetProperty(selection.CurrentFrame, frame)
            .SetWorkingLayer(recordCelSelectionPreference: true)
            .CommitToLatest();
    }

    public static void Toggle(Entity layer)
    {
        var selection = layer.Document.Get<SelectionManager>();
        var layers = selection.WorkingLayers.Value;
        if (layers.IsEmpty)
        {
            SelectOnly(layer);
            return;
        }
        if (layers[0] == layer)
            return;

        new CommandBuilder("Select Layers", layer.Document)
            .SetWorkingLayer(layers: layers.Contains(layer) ? layers.Remove(layer) : layers.Add(layer))
            .CommitToLatest();
    }

    public static ImmutableArray<Entity> ContextLayers(Entity target)
    {
        var selected = target.Document.Get<SelectionManager>().WorkingLayers.Value;
        return selected.Contains(target) ? selected : [target];
    }

    public static void ShowSelection(CheckButton button, bool primary, bool selected)
    {
        button.AddThemeIconOverride("checked", primary ? BrushIcon : CheckIcon);
        button.SetPressedNoSignal(selected);
    }

    public static Observable<T> ObservePrimary<T>(SelectionManager selection,
        Func<CommonLayerSetting, ReactiveProperty<T>> property, T empty = default) => selection.WorkingLayer
        .Select(layer => layer.IsNull ? Observable.Return(empty) : property(layer.Get<CommonLayerSetting>()).AsObservable())
        .Switch();

    public static void SetProperty<T>(string action, ImmutableArray<Entity> layers,
        Func<CommonLayerSetting, ReactiveProperty<T>> property, T value, bool sequence = false)
    {
        if (layers.IsEmpty) return;
        var command = new CommandBuilder(action, layers[0].Document);
        foreach (var layer in layers)
        {
            var setting = property(layer.Get<CommonLayerSetting>());
            if (!EqualityComparer<T>.Default.Equals(setting.Value, value))
                command.SetTarget(layer).SetProperty(setting, value);
        }
        if (sequence) command.CommitSequence();
        else command.Commit();
    }
}
