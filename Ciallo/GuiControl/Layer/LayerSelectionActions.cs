using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
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
        int frame = selection.ComputeFrameForPrimaryLayerSelection(layer);
        var parent = layer.Get<LayerTreeNode>().ParentValue;
        bool sameTemplate = parent.IsNull || !parent.Tagged<CelTag>()
            || parent.Get<LayerTreeNode>().ParentValue.Get<FolderLayerSetting>()
                .PreferredNamesForCelSelection.Value.SequenceEqual([layer.Get<CommonLayerSetting>().Name.Value]);
        if (selection.SelectedLayers.Value.Length == 1 && selection.PrimaryLayer.CurrentValue == layer
            && frame == selection.CurrentFrame.Value && sameTemplate)
            return;

        new CommandBuilder("Select Layer", layer)
            .SetProperty(selection.CurrentFrame, frame)
            .SelectLayers(recordCelSelectionPreference: true)
            .CommitToLatest();
    }

    public static void Toggle(Entity layer)
    {
        var selection = layer.Document.Get<SelectionManager>();
        var layers = selection.SelectedLayers.Value;
        if (layers.IsEmpty)
        {
            SelectOnly(layer);
            return;
        }
        if (layers[0] == layer)
            return;

        new CommandBuilder("Select Layers", layer.Document)
            .SelectLayers(recordCelSelectionPreference: true,
                layers: layers.Contains(layer) ? layers.Remove(layer) : layers.Add(layer))
            .CommitToLatest();
    }

    public static ImmutableArray<Entity> ContextLayers(Entity target)
    {
        var selected = target.Document.Get<SelectionManager>().SelectedLayers.Value;
        return selected.Contains(target) ? selected : [target];
    }

    public static void ShowSelection(CheckButton button, bool primary, bool selected)
    {
        button.AddThemeIconOverride("checked", primary ? BrushIcon : CheckIcon);
        button.SetPressedNoSignal(selected);
    }

    public static Observable<T> ObservePrimary<T>(SelectionManager selection,
        Func<CommonLayerSetting, ReactiveProperty<T>> property, T empty = default) => selection.PrimaryLayer
        .Select(layer => layer.IsNull ? Observable.Return(empty) : property(layer.Get<CommonLayerSetting>()).AsObservable())
        .Switch();

    public static void SetProperty<T>(string action, ImmutableArray<Entity> layers,
        Func<CommonLayerSetting, ReactiveProperty<T>> property, T value, bool sequence = false)
    {
        var command = CreatePropertyCommand(new CommandBuilder(action), layers, property, value);
        if (sequence) command.CommitSequence();
        else command.Commit();
    }

    public static void Rename(Entity layer, string name) =>
        SetProperty("Rename Layer", [layer], setting => setting.Name, name);

    public static void SetVisible(ImmutableArray<Entity> layers, bool visible) =>
        CreatePropertyCommand(new CommandBuilder(), layers, setting => setting.IsVisible, visible)
            .CommitOpenSequence(HistorySequenceKind.LayerVisibility);

    private static CommandBuilder CreatePropertyCommand<T>(CommandBuilder command, ImmutableArray<Entity> layers,
        Func<CommonLayerSetting, ReactiveProperty<T>> property, T value)
    {
        foreach (var layer in layers)
        {
            var setting = property(layer.Get<CommonLayerSetting>());
            if (!EqualityComparer<T>.Default.Equals(setting.Value, value))
                command.SetTarget(layer).SetProperty(setting, value);
        }
        return command;
    }
}
