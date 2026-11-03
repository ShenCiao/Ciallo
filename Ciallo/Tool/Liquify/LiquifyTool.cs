using System.Collections.Immutable;
using System.Linq;
using System.Runtime.Serialization;
using Ciallo.Data;
using Ciallo.Widget;
using Frent;
using Godot;
using ObservableCollections;
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
    public readonly ReactiveProperty<float> RadialStrength = new(0.5f);
    [DataMember]
    public readonly ReactiveProperty<float> ThicknessStrength = new(0.5f);
    [DataMember]
    public readonly ReactiveProperty<float> PressureStrength = new(0.5f);
    [DataMember]
    public readonly ReactiveProperty<bool> ShowSelectionWireframe = new(true);

    internal ReactiveProperty<float> StrengthFor(LiquifyMode mode) => mode switch
    {
        LiquifyMode.Radial => RadialStrength,
        LiquifyMode.Thickness => ThicknessStrength,
        LiquifyMode.Pressure => PressureStrength,
        _ => Strength,
    };

    // Retired one-direction modes become the signed mode in their direction.
    [OnDeserialized]
    private void RestoreRetiredModes(StreamingContext context)
    {
        switch ((int)Mode.Value)
        {
            case 2:
                Mode.Value = LiquifyMode.Radial;
                RadialStrength.Value = -Strength.Value;
                break;
            case 4:
                Mode.Value = LiquifyMode.Thickness;
                ThicknessStrength.Value = -Strength.Value;
                break;
        }
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
        layers.All(layer => layer.Has<ShapeLayerSetting>());

    public void DrawPropertyBeforeSubstates(PropertyContainer container)
    {
        container.AddProperty("Mode",
            new OptionButton()
            {
                CustomMinimumSize = new(128, 32),
                FitToLongestItem = false,
            }.BindEnum(Mode, mode => mode switch
            {
                LiquifyMode.Radial => "Pinch / Expand",
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
            .VisibleIf(Mode, LiquifyMode.Push);

        DrawAdjustment(container, LiquifyMode.Radial, "Pinch (−)", "Expand (+)");
        DrawAdjustment(container, LiquifyMode.Thickness, "Thin (−)", "Thicken (+)");
        DrawAdjustment(container, LiquifyMode.Pressure, "Decrease pressure (−)", "Increase pressure (+)");

        container.AddChild(new Label
        {
            Text = "[Pressure Adjustment Hint]",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        }.VisibleIf(Mode, LiquifyMode.Pressure));

        DrawSelectionTarget(container);
    }

    // Names what the next stroke affects, because the whole-layer default has no canvas outline.
    private void DrawSelectionTarget(PropertyContainer container)
    {
        var selectedShapes = Document.Get<SelectionManager>().SelectedShapes;
        var box = container.CreateBox().AddToChildOf(container);
        box.Name = "LiquifySelectionTarget";

        // Composed with a count, so it is translated here and rebuilt when the language changes.
        var target = new Label
        {
            AutoTranslateMode = Node.AutoTranslateModeEnum.Disabled,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        }.AddToChildOf(box);

        var deselect = container.CreateButton("Deselect").AddToChildOf(box);
        deselect.Pressed += selectedShapes.Clear;

        container.CreatePropertyBox("Show selection wireframe",
            new CheckButton
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                CustomMinimumSize = new(128, 0),
            }.BindBool(ShowSelectionWireframe)).AddToChildOf(box);

        void Refresh()
        {
            int count = selectedShapes.Count(LiquifyTargetScope.CanLiquify);
            target.Text = selectedShapes.Count > 0
                ? string.Format("Target: {0} selected shapes".Tr(), count)
                : "Target: all shapes in selected layers".Tr();
            deselect.Disabled = selectedShapes.Count == 0;
        }

        Refresh();
        selectedShapes.ObserveChanged().Subscribe(_ => Refresh()).AddTo(target);
        // Deferred so TranslationServer has switched locale before the text is rebuilt.
        var deferredRefresh = Callable.From(Refresh);
        AppPreference.Language.Skip(1).Subscribe(_ => deferredRefresh.CallDeferred()).AddTo(target);
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
            TooltipText = "[Adjustment Strength Tooltip]",
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
