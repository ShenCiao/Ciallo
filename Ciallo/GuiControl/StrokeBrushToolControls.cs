using System;
using Ciallo.Command;
using Ciallo.Data;
using Ciallo.Widget;
using Frent;
using Godot;
using R3;

namespace Ciallo.GuiControl;

internal static class StrokeBrushToolControls
{
    public static SpinSlider CreateRadius(Entity document)
    {
        var slider = BrushRadiusControls.CreateSlider();
        slider.Name = "StrokeRadius";
        return slider.BindNumber(BindWorkingValue(slider, document,
            setting => setting.IndependentRadius, document.Get<BrushManager>().CurrentStrokeRadius, "Radius"));
    }

    public static ColorPickerButton CreateColor(Entity document)
    {
        var button = new ColorPickerButton { Name = "StrokeColor", CustomMinimumSize = new(0, 32) };
        button.GetPicker().DeferredMode = true;
        var selected = document.Get<SelectionManager>().WorkingStrokeBrush;
        var global = document.Get<ColorManager>().CurrentColor;
        selected.Select(brush =>
        {
            if (brush.IsNull) return global.AsObservable();
            var setting = brush.Get<StrokeBrushSetting>();
            return setting.Color.CombineLatest(setting.ActiveBrushFlags, global,
                (_, _, color) => setting.ResolveColor(color));
        }).Switch().Subscribe(color => button.Color = color).AddTo(button);
        var history = document.Get<CommandManager>();
        Entity lastEditedBrush = default;
        long lastEditEpoch = -1;
        button.ColorChanged += color =>
        {
            var brush = selected.Value;
            var command = new CommandBuilder("Set Stroke Color", document);
            bool materialChanged = false;
            if (brush.IsNull)
                command.SetProperty(global, color);
            else
            {
                var setting = brush.Get<StrokeBrushSetting>();
                var flags = setting.ActiveBrushFlags.Value;
                bool shapeRgb = flags.HasFlag(BrushFlags.UseShapeRgb);
                bool shapeAlpha = flags.HasFlag(BrushFlags.UseShapeAlpha);
                var materialColor = new Color(shapeRgb ? setting.Color.Value : color)
                {
                    A = shapeAlpha ? setting.Color.Value.A : color.A,
                };
                var globalColor = new Color(shapeRgb ? color : global.Value)
                {
                    A = shapeAlpha ? color.A : global.Value.A,
                };
                materialChanged = materialColor != setting.Color.Value;
                if (materialChanged) command.SetProperty(setting.Color, materialColor);
                if (globalColor != global.Value) command.SetProperty(global, globalColor);
            }
            if (command.Commands.Count == 0) return;
            if (!materialChanged) command.CommitToLatest();
            else if (lastEditedBrush == brush && lastEditEpoch == history.PersistenceEpoch)
                command.CommitSequence();
            else command.Commit();
            lastEditedBrush = brush;
            lastEditEpoch = history.PersistenceEpoch;
        };
        DocumentColorControls.BindSwatches(button, document);
        return button;
    }

    private static ReactiveProperty<T> BindWorkingValue<T>(Control control, Entity document,
        Func<StrokeBrushSetting, ReactiveProperty<T?>> independent, ReactiveProperty<T> global, string name)
        where T : struct
    {
        var own = document.Get<SelectionManager>().WorkingStrokeBrush
            .Select(brush => brush.IsNull ? null : independent(brush.Get<StrokeBrushSetting>()))
            .ToReadOnlyReactiveProperty().AddTo(control);
        var value = new ReactiveProperty<T>().AddTo(control);
        bool updating = false;
        own.Select(property => property is null ? Observable.Return<T?>(null) : property.AsObservable())
            .Switch().CombineLatest(global, (local, shared) => local ?? shared)
            .Subscribe(current =>
            {
                updating = true;
                value.Value = current;
                updating = false;
            }).AddTo(control);

        var history = document.Get<CommandManager>();
        ReactiveProperty<T?> lastEditedProperty = null;
        long lastEditEpoch = -1;
        value.Skip(1).Subscribe(current =>
        {
            if (updating) return;
            var property = own.CurrentValue;
            if (property?.Value.HasValue == true)
            {
                var command = new CommandBuilder("Set Independent Brush " + name, document)
                    .SetProperty(property, (T?)current);
                if (ReferenceEquals(lastEditedProperty, property) && lastEditEpoch == history.PersistenceEpoch)
                    command.CommitSequence();
                else
                    command.Commit();
                lastEditedProperty = property;
                lastEditEpoch = history.PersistenceEpoch;
            }
            else
                new CommandBuilder("Set Global Stroke " + name, document)
                    .SetProperty(global, current).CommitToLatest();
        }).AddTo(control);
        return value;
    }
}
