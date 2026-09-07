using System.Collections.Immutable;
using Frent;
using Stateless;

namespace Ciallo.Tool;

using StateMachine = StateMachine<InteractionState, Trigger>;

// Root of the interaction hierarchy, and the resting state whenever the context resolves to no tool.
//
// Most tools use [RequestedByToolButton] for generated routing. Bucket Fill has explicit sibling
// scopes because its shared button selects between independent marker and polygon interactions.
[RegisterState]
public partial class GlobalInteractiveScope : InteractionScope
{
    [Substate]
    internal NoDocument NoDocument;

    [Substate]
    internal TimelineRolling TimelineRolling;

    [Substate]
    internal VectorFillTool VectorFill;

    [Substate]
    internal VectorFillLayerCreationTool VectorFillLayerCreation;

    [Substate]
    internal BucketFillTool BucketFill;

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
        if (toolButton is null || !InteractionManager.HasUsableLayers(layers)) return this;

        if (toolButton == ToolButton.Type.BucketFill)
        {
            return BucketFillOptions.Mode.Value switch
            {
                BucketFillOutput.Marker when VectorFillTool.CanHandleLayers(layers) => VectorFill,
                BucketFillOutput.Marker when VectorFillLayerCreationTool.CanHandleLayers(layers) =>
                    VectorFillLayerCreation,
                BucketFillOutput.Marker => this,
                BucketFillOutput.Polygon => BucketFillTool.CanHandleLayers(layers) ? BucketFill : this,
                _ => throw new System.ArgumentOutOfRangeException(nameof(BucketFillOptions.Mode)),
            };
        }

        return ResolveToolButton(toolButton.Value, layers) ?? this;
    }

    private partial InteractionState ResolveToolButton(
        ToolButton.Type toolButton,
        ImmutableArray<Entity> layers);
}
