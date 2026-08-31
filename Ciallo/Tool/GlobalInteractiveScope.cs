using System.Collections.Immutable;
using Frent;
using Stateless;

namespace Ciallo.Tool;

using StateMachine = StateMachine<InteractionState, Trigger>;

// Root of the interaction hierarchy, and the resting state whenever the context resolves to no tool.
//
// The tools are not listed here: each reaches this scope by carrying [RequestedByToolButton] on its own
// class, and the generator wires it as a substate. Adding a button-driven tool touches only that tool's
// file — search RequestedByToolButton for the full set. Declare a [Substate] field below only for a
// state this scope must reach some other way.
[RegisterState]
public partial class GlobalInteractiveScope : InteractionScope
{
    [Substate]
    internal NoDocument NoDocument;

    [Substate]
    internal TimelineRolling TimelineRolling;

    public override void ConfigureStateMachine(StateMachine sm)
    {
        sm.Configure(NoDocument)
            .Ignore(InteractionManager.ToolButtonSwitch.Trigger)
            .PermitDynamic(
                InteractionManager.DocumentOpened,
                (_, layers) => ResolveContext(ToolButton.ActiveToolButton.Value, layers));
        sm.Configure(TimelineRolling)
            .Ignore(InteractionManager.ToolButtonSwitch.Trigger)
            // Reentry, not .Ignore: OnTransitioned runs only for real transitions, and .Ignore would
            // leave InteractionManager.WorkingLayers stale.
            .PermitReentry(InteractionManager.WorkingLayersChanged.Trigger);
        sm.Configure(this)
            .Permit(InteractionManager.DocumentClosed, NoDocument)
            .PermitDynamic(
                InteractionManager.ToolButtonSwitch,
                button => ResolveContext(button, InteractionManager.WorkingLayers))
            // The fallback for layer changes: reached when no substate handles the trigger. An
            // ILayerDependent tool's generated reentry wins while that tool still accepts the incoming
            // layers; once its guard fails, Stateless falls through here and the tool switches.
            .PermitDynamic(
                InteractionManager.WorkingLayersChanged,
                layers => ResolveContext(ToolButton.ActiveToolButton.Value, layers))
            .PermitDynamic(
                InteractionManager.TimelineRollingChanged,
                rolling => rolling
                    ? TimelineRolling
                    : ResolveContext(
                        ToolButton.ActiveToolButton.Value,
                        InteractionManager.WorkingLayers));
    }

    // The one place context becomes a concrete tool. Liveness is settled here so CanHandleLayers only
    // has to judge fit; arity is not, which is how a multi-layer tool opts in.
    private InteractionState ResolveContext(
        ToolButton.Type? toolButton,
        ImmutableArray<Entity> layers)
    {
        if (toolButton is null || layers.IsEmpty) return this;

        foreach (var layer in layers)
        {
            if (layer.IsDyingOrDead || layer.IsDocument) return this;
        }

        return ResolveToolButton(toolButton.Value, layers) ?? this;
    }

    // Called by the generated reentry guards, always with the trigger's layers: at guard time
    // InteractionManager.WorkingLayers still holds the pre-transition snapshot.
    internal bool ResolvesTo(InteractionState tool, ImmutableArray<Entity> layers) =>
        ReferenceEquals(ResolveContext(ToolButton.ActiveToolButton.Value, layers), tool);

    private partial InteractionState ResolveToolButton(
        ToolButton.Type toolButton,
        ImmutableArray<Entity> layers);
}
