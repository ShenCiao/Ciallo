using System.Collections.Immutable;
using Ciallo.Widget;
using Frent;
using Stateless;

namespace Ciallo.Tool;

using StateMachine = StateMachine<InteractionState, Trigger>;

// Root of the interaction hierarchy, and the resting state whenever the context resolves to no tool.
//
// Most tools use [RequestedByToolButton] for generated routing. Bucket Fill has explicit sibling
// scopes because its shared button selects between independent marker and polygon interactions.
[RegisterState]
public partial class GlobalInteractiveScope : InteractionScope, IPropertyProvider
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

    public void DrawPropertyBeforeSubstates(PropertyContainer container)
    {
        // Temporary GUI hack: InteractionPropertyTree has no cross-scope property-section host.
        // Tool controls are hidden with their state branch, but this selector must remain reachable
        // when no Bucket Fill scope accepts the layer. TODO: Move it to a shared property section
        // when that infrastructure exists; its visibility must be independent of tool activation.
        var mode = AppPreference.BucketFill.DrawModeProperty(container);
        void Refresh() => mode.Visible = InteractionManager.StateMachine.State == this &&
            ToolButton.ActiveToolButton.Value == ToolButton.Type.BucketFill;
        InteractionManager.StateChanged += Refresh;
        container.TreeExiting += () => InteractionManager.StateChanged -= Refresh;
        Refresh();
    }

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
            // leave InteractionManager.SelectedLayers stale.
            .PermitReentry(InteractionManager.SelectedLayersChanged.Trigger);
        sm.Configure(this)
            .Permit(InteractionManager.DocumentClosed, NoDocument)
            .PermitDynamic(
                InteractionManager.ToolButtonSwitch,
                button => ResolveContext(button, InteractionManager.SelectedLayers))
            // The fallback for layer changes: reached when no substate handles the trigger. An
            // ILayerDependent tool's generated reentry wins while that tool still accepts the incoming
            // layers; once its guard fails, Stateless falls through here and the tool switches.
            .PermitDynamic(
                InteractionManager.SelectedLayersChanged,
                layers => ResolveContext(ToolButton.ActiveToolButton.Value, layers))
            .PermitDynamic(
                InteractionManager.TimelineRollingChanged,
                rolling => rolling
                    ? TimelineRolling
                    : ResolveContext(
                        ToolButton.ActiveToolButton.Value,
                        InteractionManager.SelectedLayers));
    }

    // The one place context becomes a concrete tool. Liveness is settled here so CanHandleLayers only
    // has to judge fit. Current tools use the first selected layer as their drawing target.
    private InteractionState ResolveContext(
        ToolButton.Type? toolButton,
        ImmutableArray<Entity> layers)
    {
        if (toolButton is null || !InteractionManager.HasUsableLayers(layers)) return this;

        if (toolButton == ToolButton.Type.BucketFill)
        {
            return AppPreference.BucketFill.Mode.Value switch
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
