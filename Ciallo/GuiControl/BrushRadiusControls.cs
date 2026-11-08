using Ciallo.Command;
using Ciallo.Data;
using Ciallo.Widget;
using Frent;
using Godot;
using R3;

namespace Ciallo.GuiControl;

internal static class BrushRadiusControls
{
    private static SpinSlider CreateSlider() => new()
    {
        MinValue = 0.01,
        MaxValue = 256,
        Step = 0.01,
        AllowGreater = true,
        ExpEdit = true,
    };

    public static Observable<float> ObserveCurrentRadius(Entity document = default) => document.IsNull
        ? AppDocumentManager.WorkingDocument.Select(current => current.IsNull
            ? Observable.Return(BrushManager.DefaultStrokeRadius)
            : current.Get<BrushManager>().CurrentStrokeRadius.AsObservable()).Switch()
        : document.Get<BrushManager>().CurrentStrokeRadius.AsObservable();

    public static SpinSlider CreateGlobalRadius(Entity document)
    {
        var slider = CreateSlider();
        slider.Name = "GlobalStrokeRadius";
        var radius = document.Get<BrushManager>().CurrentStrokeRadius;
        radius.Subscribe(value => slider.SetValueNoSignal(value)).AddTo(slider);
        slider.ValueChanged += (_, value) =>
        {
            if (radius.Value == (float)value) return;
            new CommandBuilder("Set Global Stroke Radius", document)
                .SetProperty(radius, (float)value).CommitToLatest();
        };
        return slider;
    }

    public static Container CreateIndependentRadius(PropertyContainer container,
        ReactiveProperty<float?> radius, Entity document = default)
    {
        var slider = CreateSlider();
        slider.Name = "IndependentRadiusValue";
        var enabled = new CheckBox
        {
            Name = "IndependentRadiusEnabled",
            TooltipText = "[Independent Radius Tooltip]",
        };
        var row = container.CreateCheckBoxCombo("Independent radius", enabled, slider);
        row.Name = "IndependentRadius";
        radius.CombineLatest(ObserveCurrentRadius(document), (own, global) => (own, global))
            .Subscribe(values =>
            {
                enabled.SetPressedNoSignal(values.own.HasValue);
                slider.SetValueNoSignal(values.own ?? values.global);
                slider.Visible = values.own.HasValue;
            }).AddTo(row);
        enabled.Toggled += value => SetRadius(value ? (float)slider.Value : null);
        slider.ValueChanged += (_, value) => SetRadius((float)value);
        return row;

        void SetRadius(float? value)
        {
            if (radius.Value == value) return;
            if (document.IsNull)
                radius.Value = value;
            else
            {
                var brush = document.Get<SelectionManager>().WorkingStrokeBrush.Value;
                new CommandBuilder("Set Independent Brush Radius", brush)
                    .SetProperty(e => e.Get<StrokeBrushSetting>().IndependentRadius, value).CommitSequence();
            }
        }
    }
}
