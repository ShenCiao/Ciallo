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
    public static Container CreateIndependentChannels<TFlags>(PropertyContainer container,
        ReactiveProperty<Color> color, ReactiveProperty<TFlags> flags,
        TFlags useShapeRgb, TFlags useShapeAlpha, string alphaLabel, Entity document = default)
        where TFlags : Enum
    {
        var rows = container.CreateBox();
        rows.Name = "IndependentChannels";
        var button = new ColorPickerButton
        {
            Name = "IndependentColorValue",
            CustomMinimumSize = new(32, 32),
        };
        button.GetPicker().ColorMode = ColorPicker.ColorModeType.Rgb;
        button.GetPicker().EditAlpha = false;
        button.GetPicker().DeferredMode = true;
        BindSwatches(button, document);
        color.Subscribe(value => button.Color = new(value) { A = 1 }).AddTo(button);
        // Swatches and pasted RGBA values must not change the independently edited alpha.
        button.ColorChanged += value => color.Value = new(value) { A = color.Value.A };
        AddChannel("Independent color", "IndependentColor", useShapeRgb, button);

        var alpha = new SpinSlider
        {
            Name = "IndependentAlphaValue", MinValue = 0, MaxValue = 1, Step = 0.01,
        };
        alpha.BindNumber(color.Project(value => value.A, value => new Color(color.Value) { A = value }).AddTo(alpha));
        AddChannel(alphaLabel, "IndependentAlpha", useShapeAlpha, alpha);
        return rows;

        void AddChannel(string label, string name, TFlags mask, Control value)
        {
            var independent = flags.Project(current => !current.HasFlag(mask), enabled => enabled
                ? (TFlags)((dynamic)flags.Value & ~(dynamic)mask)
                : (TFlags)((dynamic)flags.Value | (dynamic)mask)).AddTo(rows);
            var check = new CheckBox { Name = name + "Enabled" }.BindBool(independent);
            var row = container.CreateCheckBoxCombo(label, check, value);
            row.Name = name;
            value.VisibleIf(independent, enabled => enabled);
            rows.AddChild(row);
        }
    }

    public static Container CreateFillBrushControls(PropertyContainer container, Entity document)
    {
        var selected = document.Get<SelectionManager>().WorkingVectorFillBrush;
        var lifetime = container.CreateBox();
        var color = BindBrushProperty(lifetime, document, selected, e => e.Get<FillBrushSetting>().FillColor, "Color");
        var flags = BindBrushProperty(lifetime, document, selected, e => e.Get<FillBrushSetting>().ActiveBrushFlags, "Color Sources");
        lifetime.AddChild(CreateIndependentChannels(container, color, flags,
            FillBrushFlags.UseShapeRgb, FillBrushFlags.UseShapeAlpha, "Independent opacity", document));
        return lifetime.VisibleIf(selected, Entity.IsNotNull);
    }

    private static ReactiveProperty<T> BindBrushProperty<T>(Control lifetime, Entity document,
        ReactiveProperty<Entity> selected, Func<Entity, ReactiveProperty<T>> getProperty, string name)
    {
        var property = selected.Select(brush => brush.IsNull ? null : getProperty(brush))
            .ToReadOnlyReactiveProperty().AddTo(lifetime);
        var proxy = new ReactiveProperty<T>().AddTo(lifetime);
        bool updating = false;
        property.Select(current => current is null ? Observable.Return(default(T)) : current.AsObservable()).Switch()
            .Subscribe(value =>
            {
                updating = true;
                proxy.Value = value;
                updating = false;
            }).AddTo(lifetime);
        var history = document.Get<CommandManager>();
        ReactiveProperty<T> lastEditedProperty = null;
        long lastEditEpoch = -1;
        proxy.Skip(1).Subscribe(value =>
        {
            if (updating) return;
            var current = property.CurrentValue;
            var command = new CommandBuilder("Edit Fill Brush " + name, document).SetProperty(current, value);
            if (ReferenceEquals(lastEditedProperty, current) && lastEditEpoch == history.PersistenceEpoch)
                command.CommitSequence();
            else command.Commit();
            lastEditedProperty = current;
            lastEditEpoch = history.PersistenceEpoch;
        }).AddTo(lifetime);
        return proxy;
    }

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
