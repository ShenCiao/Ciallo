using System.Collections.Generic;
using System.Collections.Immutable;
using Ciallo.Data;
using Ciallo.Rendering;
using Frent;
using Godot;
using R3;
using Stateless;

namespace Ciallo.Tool;

using StateMachine = StateMachine<InteractionState, Trigger>;

[RegisterState]
public class VectorFillTool : InteractionScope, ILayerDependent
{
    [Substate]
    internal VectorFillHover Hover;

    [Substate]
    internal PaintVectorFillMarkerInteractor Left;

    public readonly Subject<Unit> DeactivateSignal = new();

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
        layers[0].Has<VectorFillLayerSetting>();

    protected override void OnActivated()
    {
        if (!WorkingLayer.Has<VectorFillLayerSetting>()) return;
        WorkingLayer.Get<OverlayHolder>().Visible = true;

        var referenceLayers = WorkingLayer.Get<VectorFillLayerSetting>().ReferenceLayers;
        AppPreference.ShowVectorFillReferenceLayerWireframe
            .TakeUntil(DeactivateSignal)
            .Subscribe(visible => SetWireframeVisibility(referenceLayers, visible),
                _ => SetWireframeVisibility(referenceLayers, false));
    }

    protected override void OnDeactivated()
    {
        DeactivateSignal.OnNext(Unit.Default);
        if (!WorkingLayer.Has<VectorFillLayerSetting>()) return;
        WorkingLayer.Get<OverlayHolder>().Visible = false;
    }

    public static void SetWireframeVisibility(IEnumerable<Entity> list, bool visible)
    {
        foreach (var e in list)
        {
            foreach (var n in e.Get<OverlayHolder>().GetChildren())
            {
                var node = (PolylineWireframe)n;
                node.Visible = visible;
                node.Dots.Visible = !visible;
            }
        }
    }
}
