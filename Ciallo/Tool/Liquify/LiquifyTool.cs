using System.Collections.Immutable;
using System.Runtime.Serialization;
using Ciallo.Data;
using Ciallo.Widget;
using Frent;
using Godot;
using R3;
using Stateless;

namespace Ciallo.Tool;

using StateMachine = StateMachine<InteractionState, Trigger>;

[DataContract, RegisterState]
[RequestedByToolButton(ToolButton.Type.Liquify)]
public class LiquifyTool : InteractionScope, IPropertyProvider, ILayerDependent
{
    [DataMember]
    public readonly ReactiveProperty<LiquifyMode> Mode = new(LiquifyMode.Push);
    [DataMember]
    public readonly ReactiveProperty<float> Radius = new(64f);
    [DataMember]
    public readonly ReactiveProperty<float> Strength = new(0.5f);
    [DataMember]
    public readonly ReactiveProperty<float> ThicknessStrength = new(0.5f);
    [DataMember]
    public readonly ReactiveProperty<float> PressureStrength = new(0.5f);

    internal ReactiveProperty<float> StrengthFor(LiquifyMode mode) => mode switch
    {
        LiquifyMode.Thickness => ThicknessStrength,
        LiquifyMode.Pressure => PressureStrength,
        _ => Strength,
    };

    [OnDeserialized]
    private void RestoreFormerThinMode(StreamingContext context)
    {
        if ((int)Mode.Value != 4) return;
        Mode.Value = LiquifyMode.Thickness;
        ThicknessStrength.Value = -Strength.Value;
    }

    [Substate]
    internal LiquifyHover Hover;

    [Substate]
    internal LiquifyInteractor Left;

    public override void ConfigureStateMachine(StateMachine sm)
    {
        sm.Configure(this)
            .InitialTransition(Hover);
        sm.Configure(Hover)
            .Permit(Trigger.Press(MouseButton.Left), Left)
            .PermitReentry(Trigger.Refresh);
        sm.Configure(Left)
            .Permit(Trigger.Release(MouseButton.Left), Hover)
            .PermitStandardExits(Hover);
    }

    public static bool CanHandleLayers(ImmutableArray<Entity> layers) =>
        layers[0].Has<ShapeLayerSetting>();

    public void DrawPropertyBeforeSubstates(PropertyContainer container)
    {
        container.AddProperty("Mode",
            new OptionButton()
            {
                CustomMinimumSize = new(128, 32),
                FitToLongestItem = false,
            }.BindEnum(Mode, mode => mode switch
            {
                LiquifyMode.Thickness => "Thickness adjustment",
                LiquifyMode.Pressure => "Pressure adjustment",
                _ => mode.ToString(),
            }));

        container.AddProperty("Radius",
            new SpinSlider
            {
                MinValue = 1f,
                MaxValue = 512f,
                Step = 1f,
                ExpEdit = true,
            }.BindNumber(Radius));

        container.AddProperty("Strength",
            new SpinSlider
            {
                MinValue = 0.01f,
                MaxValue = 1f,
                Step = 0.01f,
                AllowGreater = true,
            }.BindNumber(Strength))
            .VisibleIf(Mode, mode => mode is not (LiquifyMode.Thickness or LiquifyMode.Pressure));

        DrawAdjustment(container, LiquifyMode.Thickness, "Thin (−)", "Thicken (+)");
        DrawAdjustment(container, LiquifyMode.Pressure, "Decrease pressure (−)", "Increase pressure (+)");

        container.AddChild(new Label
        {
            Text = "Edits recorded pressure without changing thickness. Brushes without pressure-driven flow may show no visible change.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        }.VisibleIf(Mode, LiquifyMode.Pressure));
    }

    private void DrawAdjustment(PropertyContainer container, LiquifyMode mode, string negative, string positive)
    {
        var strength = StrengthFor(mode);
        var box = new VBoxContainer();
        var slider = new SpinSlider
        {
            Name = $"Liquify{mode}Strength",
            MinValue = -1f,
            MaxValue = 1f,
            Step = 0.01f,
            TooltipText = "Negative values decrease; positive values increase. Zero has no effect.",
        }.BindNumber(strength);
        slider.Ready += () =>
        {
            slider.Slider.TickCount = 3;
            slider.Slider.TicksOnBorders = true;
        };
        box.AddChild(slider);

        var directions = new HBoxContainer();
        directions.AddChild(new Label { Text = negative, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        directions.AddChild(new Label { Text = positive });
        box.AddChild(directions);

        var reverse = container.CreateButton("Reverse direction");
        reverse.Pressed += () => strength.Value = -strength.Value;
        box.AddChild(reverse);
        container.AddProperty("Adjustment strength", box).VisibleIf(Mode, mode);
    }
}
