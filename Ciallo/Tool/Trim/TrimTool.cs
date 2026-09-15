using System;
using System.Collections.Immutable;
using Ciallo.Data;
using Ciallo.Geometry;
using Frent;
using Godot;
using R3;
using Stateless;

namespace Ciallo.Tool;

using StateMachine = StateMachine<InteractionState, Trigger>;

/// <summary>
/// Trim tool aims to be "visually/feelingly topologically robust"
/// Not truly topologically correct, as long as users feel it correct.
/// Ciallo is a drawing app, not a CAD topology editor: prefer visually
/// correct 95% behavior over preserving every tiny real stroke or junction.
/// </summary>
[RegisterState]
[RequestedByToolButton(ToolButton.Type.Trim)]
public class TrimTool : InteractionScope, ILayerDependent
{
    [Substate]
    internal TrimHover Hover;

    [Substate]
    internal TrimInteractor Trim;

    // Layer-owned ArrangementManager, shared with vector-fill and future topology tools.
    public ArrangementManager Arrangement { get; private set; }

    private IDisposable _arrReadySub;

    public override void ConfigureStateMachine(StateMachine sm)
    {
        sm.Configure(this)
            .InitialTransition(Hover);
        sm.Configure(Hover)
            .PermitIf(Trigger.Press(MouseButton.Left), Trim, () => Arrangement?.ArrReady.CurrentValue != null)
            .PermitReentry(Trigger.Refresh);
        sm.Configure(Trim)
            .Permit(Trigger.Release(MouseButton.Left), Hover)
            .PermitStandardExits(Hover);
    }

    public static bool CanHandleLayers(ImmutableArray<Entity> layers) =>
        (layers[0].Has<ShapeLayerSetting>() || layers[0].Has<VectorFillLayerSetting>());

    protected override void OnActivated()
    {
        Arrangement = PrimaryLayer.Get<ArrangementManager>();

        _arrReadySub = Arrangement.ArrReady.Subscribe(_ =>
        {
            if (InteractionManager.StateMachine.State is TrimHover hover)
                hover.RefreshCursor();
        });
    }

    protected override void OnDeactivated()
    {
        _arrReadySub?.Dispose();
        _arrReadySub = null;
        Arrangement = null;
    }
}
