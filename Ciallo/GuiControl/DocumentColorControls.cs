using System;
using System.Collections.Immutable;
using System.Linq;
using Ciallo.Command;
using Ciallo.Data;
using Ciallo.Widget;
using Frent;
using Godot;
using R3;

namespace Ciallo.GuiControl;

internal static class DocumentColorControls
{
    public static void BindCurrentColor(ColorPicker picker, Entity document)
    {
        var colors = document.Get<ColorManager>();
        colors.CurrentColor.Subscribe(color => picker.Color = color).AddTo(picker);
        picker.DeferredMode = true;
        BindSwatches(picker, document);
        picker.ColorChanged += color =>
        {
            if (colors.CurrentColor.Value == color) return;
            new CommandBuilder("Set Current Color", document)
                .SetProperty(colors.CurrentColor, color).CommitToLatest();
        };
    }

    public static NullableColorPickerButton BindOverride(NullableColorPickerButton button, Entity document,
        ReactiveProperty<Entity> selectedBrush, Func<Entity, ReactiveProperty<Color?>> property)
    {
        selectedBrush.Select(brush => brush.IsNull
                ? Observable.Return<Color?>(null) : property(brush).AsObservable())
            .Switch().Subscribe(button.SetColorOrNullNoSignal).AddTo(button);
        button.ColorOrNullChanged.Subscribe(color =>
        {
            var brush = selectedBrush.Value;
            if (brush.IsNull || property(brush).Value == color) return;
            new CommandBuilder("Set Brush Color Override", brush)
                .SetProperty(property, color).CommitSequence();
        }).AddTo(button);
        BindSwatches(button, document);
        return button;
    }

    public static void BindSwatches(ColorPickerButton button, Entity document = default)
        => BindSwatches(button.GetPicker(), document, button);

    private static void BindSwatches(ColorPicker picker, Entity document, ColorPickerButton button = null)
    {
        var paletteDocument = document;
        var swatches = document.IsNull
            ? AppDocumentManager.WorkingDocument.Select(current =>
            {
                paletteDocument = current;
                foreach (var recent in picker.GetRecentPresets()) picker.EraseRecentPreset(recent);
                return current.IsNull ? Observable.Return(ImmutableArray<Color>.Empty)
                    : current.Get<ColorManager>().Swatches.AsObservable();
            }).Switch()
            : document.Get<ColorManager>().Swatches.AsObservable();
        bool synchronizing = false;
        var currentSwatches = ImmutableArray<Color>.Empty;
        swatches.Subscribe(value =>
        {
            currentSwatches = value;
            SynchronizeSwatches();
        }).AddTo(picker);
        // Native ColorPicker restores its shared preset cache when it enters the tree.
        if (picker.IsNodeReady()) ResetPicker();
        else picker.Ready += ResetPicker;
        if (button != null)
        {
            button.GetPopup().AboutToPopup += SynchronizeSwatches;
            button.PopupClosed += SaveSwatches;
        }
        picker.PresetAdded += _ => SaveSwatches();
        picker.PresetRemoved += _ => SaveSwatches();

        void ResetPicker()
        {
            foreach (var recent in picker.GetRecentPresets()) picker.EraseRecentPreset(recent);
            SynchronizeSwatches();
        }

        void SynchronizeSwatches()
        {
            synchronizing = true;
            foreach (var color in picker.GetPresets()) picker.ErasePreset(color);
            foreach (var color in currentSwatches) picker.AddPreset(color);
            synchronizing = false;
        }

        void SaveSwatches()
        {
            if (synchronizing || paletteDocument.IsNull) return;
            var value = picker.GetPresets().ToImmutableArray();
            var colors = paletteDocument.Get<ColorManager>();
            if (colors.Swatches.Value.SequenceEqual(value)) return;
            new CommandBuilder("Edit Color Swatches", paletteDocument)
                .SetProperty(colors.Swatches, value).Commit();
        }
    }
}
