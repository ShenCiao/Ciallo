using System.Collections.Immutable;
using Ciallo.Data;
using Frent;
using Godot;
using Stateless;

namespace Ciallo.Tool;

using StateMachine = StateMachine<InteractionState, Trigger>;

// Polygon bucket fill owns its interaction lifecycle independently of live marker filling.
[RegisterState]
public class BucketFillTool : InteractionScope, ILayerDependent
{
    [Substate]
    internal BucketFillHover Hover;

    [Substate]
    internal BucketFillInteractor Left;

    public override void ConfigureStateMachine(StateMachine sm)
    {
        sm.Configure(this)
            .InitialTransition(Hover);
        sm.Configure(Hover)
            // Keep the mode selector available on other layer types, but only capture on ShapeLayer.
            .PermitIf(Trigger.Press(MouseButton.Left), Left, () => CanFillLayers(WorkingLayers))
            .PermitReentry(Trigger.Refresh);
        sm.Configure(Left)
            .Permit(Trigger.Release(MouseButton.Left), Hover)
            .PermitStandardExits(Hover);
    }

    // Hover remains available for mode selection; CanFillLayers separately gates polygon capture.
    public static bool CanHandleLayers(ImmutableArray<Entity> layers) => true;

    internal static bool CanFillLayers(ImmutableArray<Entity> layers) =>
        layers.Length == 1 && layers[0].Has<ShapeLayerSetting>();
}
