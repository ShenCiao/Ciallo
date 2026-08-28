#nullable enable
using System.Collections.Immutable;
using Frent;
using Stateless;

namespace Ciallo.Tool;

using StateMachine = StateMachine<InteractionState, Trigger>;

// Root of the interaction hierarchy, and the "no tool available" resting state: whenever the current
// context resolves to no concrete tool, the machine rests here.
[RegisterState]
public partial class GlobalInteractiveScope : InteractionScope
{
    [Substate]
    internal NoDocument NoDocument = null!;

    [Substate]
    internal TimelineRolling TimelineRolling = null!;

    public override void ConfigureStateMachine(StateMachine sm)
    {
        sm.Configure(NoDocument)
            .Ignore(InteractionManager.ToolButtonSwitch.Trigger)
            .PermitDynamic(
                InteractionManager.DocumentOpened,
                (_, layers) => ResolveContext(ToolButton.ActiveToolButton.Value, layers));
        sm.Configure(TimelineRolling)
            // RequestTool persists selection. Reentry updates snapshot without lifecycle: .Ignore
            // would leave InteractionManager.WorkingLayers stale, because Stateless runs
            // OnTransitioned only for real transitions.
            .Ignore(InteractionManager.ToolButtonSwitch.Trigger)
            .PermitReentry(InteractionManager.WorkingLayersChanged.Trigger);
        sm.Configure(this)
            .Permit(InteractionManager.DocumentClosed, NoDocument)
            .PermitDynamic(
                InteractionManager.ToolButtonSwitch,
                button => ResolveContext(button, InteractionManager.WorkingLayers))
            // Resolves this scope's own resting case (no tool available -> a tool becomes available)
            // and any substate without its own handler for the trigger.
            //
            // An ILayerDependent tool's generated PermitReentryIf takes precedence while that tool
            // still owns the incoming layers; when its guard fails Stateless falls through to here
            // and the tool switch happens. A manual tool blocks this by configuring its own handler.
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

    // Single place where context becomes a concrete tool. Layer validity is established once here, so
    // ILayerDependent.CanHandleLayers implementations receive a usable snapshot and only decide fit.
    //
    // Layers are pre-filtered but arity is not: each tool declares the arity it accepts, which is how
    // multi-layer tools will opt in. Returns this when nothing fits.
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

    // Generated reentry guards call this with the incoming trigger parameter, never with
    // InteractionManager.WorkingLayers, which still holds the pre-transition snapshot at guard time.
    internal bool ResolvesTo(InteractionState tool, ImmutableArray<Entity> layers) =>
        ReferenceEquals(ResolveContext(ToolButton.ActiveToolButton.Value, layers), tool);

    // Generated: ordered first-match over [RequestedByToolButton] tools. Null when none fits.
    private partial InteractionState? ResolveToolButton(
        ToolButton.Type toolButton,
        ImmutableArray<Entity> layers);
}
