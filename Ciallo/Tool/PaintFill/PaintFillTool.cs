using System.Collections.Immutable;
using System.Runtime.Serialization;
using Ciallo.Data;
using Frent;
using Godot;
using R3;
using Stateless;

namespace Ciallo.Tool;

using StateMachine = StateMachine<InteractionState, Trigger>;

[DataContract, RegisterState]
[RequestedByToolButton(ToolButton.Type.PaintFill)]
public class PaintFillTool : InteractionScope, ILayerDependent
{
    [DataMember]
    public readonly ReactiveProperty<int> Mode = new(0); // 0 = Freehand, 1 = PolyCubicBezier

    [Substate]
    internal PaintFillHover Hover;

    [Substate]
    internal PaintFillInteractor Left;

    [Substate]
    internal PaintFillPolyCubicBezierInteractor PolyCubicBezier;

    public override void ConfigureStateMachine(StateMachine sm)
    {
        sm.Configure(this)
            .InitialTransition(Hover);
        sm.Configure(Hover)
            .PermitDynamic(Trigger.Press(MouseButton.Left), () => Mode.Value == 1 ? PolyCubicBezier : Left)
            .PermitReentry(Trigger.Refresh);
        sm.Configure(Left)
            .Permit(Trigger.Release(MouseButton.Left), Hover)
            .PermitStandardExits(Hover);
        sm.Configure(PolyCubicBezier)
            .Permit(PolyCubicBezierInteractor.Closed, Hover)
            .PermitStandardExits(Hover);
    }

    public static bool CanHandleLayers(ImmutableArray<Entity> layers) =>
        layers[0].Has<ShapeLayerSetting>();
}
