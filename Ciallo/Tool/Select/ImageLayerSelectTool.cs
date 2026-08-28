using System.Collections.Immutable;
using Ciallo.Data;
using Frent;
using Godot;
using Stateless;

namespace Ciallo.Tool;

using StateMachine = StateMachine<InteractionState, Trigger>;

[RegisterState]
// Image and polyline select accept disjoint layer types, so order is not observable today; declared
// anyway so adding a broader Select candidate cannot silently reorder these two.
[RequestedByToolButton(ToolButton.Type.Select, Priority = 0)]
public class ImageLayerSelectTool : InteractionScope, ILayerDependent
{
    [Substate]
    internal ImageLayerSelectHover Hover = null!;

    [Substate]
    internal ImageTransformInteractor Left = null!;

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
        layers.Length == 1 && layers[0].Has<ImageLayerSetting>();
}
