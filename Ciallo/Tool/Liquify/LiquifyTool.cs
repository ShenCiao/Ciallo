using System.Collections.Immutable;
using Ciallo.Data;
using Ciallo.Widget;
using Frent;
using Godot;
using R3;
using Stateless;

namespace Ciallo.Tool;

using StateMachine = StateMachine<InteractionState, Trigger>;

[RegisterState]
[RequestedByToolButton(ToolButton.Type.Liquify)]
public class LiquifyTool : InteractionScope, IPropertyProvider, ILayerDependent
{
    public readonly ReactiveProperty<LiquifyMode> Mode = new(LiquifyMode.Push);
    public readonly ReactiveProperty<float> Radius = new(64f);
    public readonly ReactiveProperty<float> Strength = new(0.5f);

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
            .Permit(InteractionManager.CancelRequested, Hover)
            .Permit(InteractionManager.ConfirmRequested, Hover)
            .Permit(InteractionManager.InputCaptureLost, Hover);
    }

    public static bool CanHandleLayers(ImmutableArray<Entity> layers) =>
        layers.Length == 1 && layers[0].Has<ShapeLayerSetting>();

    public void DrawPropertyBeforeSubstates(PropertyContainer container)
    {
        container.AddProperty("Mode",
            new OptionButton()
            {
                CustomMinimumSize = new(128, 32),
                FitToLongestItem = false,
            }.BindEnum(Mode));

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
            }.BindNumber(Strength));
    }
}
