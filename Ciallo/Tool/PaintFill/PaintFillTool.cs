using System.Collections.Immutable;
using Ciallo.Data;
using Frent;
using Godot;
using Stateless;

namespace Ciallo.Tool;

using StateMachine = StateMachine<InteractionState, Trigger>;

[RegisterState]
[RequestedByToolButton(ToolButton.Type.PaintFill)]
public class PaintFillTool : InteractionScope, ILayerDependent
{
    [Substate]
    internal PaintFillHover Hover = null!;

    [Substate]
    internal PaintFillInteractor Left = null!;

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
}
