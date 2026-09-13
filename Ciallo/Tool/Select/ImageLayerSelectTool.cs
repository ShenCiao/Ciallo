using System.Collections.Immutable;
using Ciallo.Data;
using Frent;
using Godot;
using Stateless;

namespace Ciallo.Tool;

using StateMachine = StateMachine<InteractionState, Trigger>;

[RegisterState]
[RequestedByToolButton(ToolButton.Type.Select)]
public class ImageLayerSelectTool : InteractionScope, ILayerDependent
{
    [Substate]
    internal ImageLayerSelectHover Hover;

    [Substate]
    internal ImageTransformInteractor Left;

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
        layers[0].Has<ImageLayerSetting>();
}
