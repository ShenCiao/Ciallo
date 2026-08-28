using System;
using System.Collections.Immutable;
using System.Linq;
using Ciallo.Command;
using Frent;
using Godot;
using Stateless;

namespace Ciallo.Tool;

using StateMachine = StateMachine<InteractionState, Trigger>;

public abstract class InteractionState
{
    // Manager-owned snapshots. States can trust these are valid (no null/availability checks needed).
    protected static Entity Document => InteractionManager.Document;
    protected ImmutableArray<Entity> WorkingLayers => InteractionManager.WorkingLayers;
    protected Entity WorkingLayer => WorkingLayers.Single();
    protected static CursorButtonData LatestCursor => InteractionManager.LatestCursor;

    // Publish semantic events. Queued: defers during active transitions, otherwise synchronous.
    // Never Fire from OnExit (would resolve from destination state). Use BeforeSourceExit instead.
    protected static void Fire(Trigger trigger) => InteractionManager.Fire(trigger);

    public virtual void OnEntry(StateMachine.Transition transition) { }
    public virtual void OnExit(StateMachine.Transition transition) { }
}

// Base for non-leaf states in the hierarchy. Super states share input handling or object lifecycle.
// OnActivated/OnDeactivated own scope-level resources: preview nodes, overlays, subscriptions.
// Per-document state (e.g., ArrangementManager reference, reactive subscriptions) is loaded from
// Document/WorkingLayers in OnActivated, stored in instance fields, and cleared in OnDeactivated.
// Scopes are process-lifetime singletons; reentry on WorkingLayersChanged refreshes document context.
public abstract class InteractionScope : InteractionState
{
    public abstract void ConfigureStateMachine(StateMachine stateMachine);

    // Scope owns document/layer-bound resources. Reentry tears down against old context then
    // rebuilds against new context. Per-document state is loaded from Document/WorkingLayers here,
    // not stored in separate per-document component instances.
    protected virtual void OnActivated() { }
    protected virtual void OnDeactivated() { }

    public sealed override void OnEntry(StateMachine.Transition transition) => OnActivated();
    public sealed override void OnExit(StateMachine.Transition transition) => OnDeactivated();
}

// Base for leaf states: one continuous stretch of user input, from entry to End or Cancel.
// Interactions publish semantic events via Fire(Trigger) but never own the machine.
public abstract class Interaction : InteractionState
{
    // Per-interaction throttling config. Read once per motion event; override the property, don't assign.
    /// <summary>
    /// Tell the tool to throttle update interval in this interaction.
    /// Set this to 0 if need raw input data.
    /// </summary>
    /// <remarks>
    /// Multiple input in one frame could cause Godot stutter.
    /// E.g. 144FPS screen, 1000Hz mouse report rate, dragging mouse could cause 6-7 input events in one frame
    /// Directly calling Polygon2D.SetPolygon to set 500 points in one frame will stutter godot.
    /// </remarks>
    public virtual TimeSpan MovingMinInterval => TimeSpan.FromMilliseconds(5);

    // Replace the `BeforeTransitionSrcEnd` in lagacy code.
    // Called on destination before source exits. Allows reading transient source state before teardown.
    // Source == this means reentry. Runs before CancelsOn/End/Cancel regardless of disposition.
    // Current use case: transfer accumulated stroke data when switching paint interactions without lifting pen.
    public virtual void BeforeSourceExit(Interaction source) { }

    // The exiting interaction decides whether this transition cancels. Transitions to scopes/sentinels
    // (not interactions) are decided by trigger identity. Hover interactions whose End is aliased to Cancel
    // never need to override this.
    protected virtual bool CancelsOn(StateMachine.Transition transition) => false;

    public abstract void Start(CursorButtonData data);
    public abstract void Moving(CursorMotionData data);
    public abstract void End(CursorButtonData data);
    public abstract void Cancel();
    public abstract bool OnKey(InputEventKey key, CursorButtonData data);
    public virtual bool OnMouseButton(InputEventMouseButton button, CursorButtonData data) => false;

    public override void OnEntry(StateMachine.Transition transition)
    {
        Start(InteractionManager.LatestCursor);
    }

    public sealed override void OnExit(StateMachine.Transition transition)
    {
        if (transition.Destination is Interaction destination &&
            ReferenceEquals(transition.Source, this))
        {
            destination.BeforeSourceExit(this);
        }

        // Refresh and normal completion both use End before destination entry. CapturingInteraction
        // should not permit Refresh unless normal End is valid.
        if (CancelsOn(transition))
        {
            Cancel();
            return;
        }

        End(InteractionManager.LatestCursor);
    }
}

// Capturing interactions consume all input by default. Override OnKey/OnMouseButton to process events.
// Default cancellation: loses context (cancel key, capture loss, document close, timeline rolling,
// tool switch, layer change). Override CancelsOn for different behavior.
public abstract class CapturingInteraction : Interaction
{
    public override bool OnKey(InputEventKey key, CursorButtonData data) => true;
    public override bool OnMouseButton(InputEventMouseButton button, CursorButtonData data) => true;

    protected override bool CancelsOn(StateMachine.Transition transition) =>
        transition.Trigger == InteractionManager.CancelRequested ||
        transition.Trigger == InteractionManager.InputCaptureLost ||
        transition.Trigger == InteractionManager.DocumentClosed ||
        transition.Trigger == InteractionManager.TimelineRollingChanged.Trigger ||
        transition.Trigger == InteractionManager.ToolButtonSwitch.Trigger ||
        transition.Trigger == InteractionManager.WorkingLayersChanged.Trigger;
}

// State hierarchy declaration attributes:
// - [RegisterState]: marks a state for registration in InteractionManager
// - [Substate]: declares child + assigns reference (implies access)
// - [StateAccess]: access-only reference without parent-child relationship
[AttributeUsage(AttributeTargets.Class)]
public sealed class RegisterStateAttribute : Attribute { }

// Tool scopes reachable from a tool-panel button. The generator emits the button-to-tool resolution
// and the substate wiring under GlobalInteractiveScope, so annotated tools need no field there.
//
// Candidates sharing one button are tried by Priority (lower first), ties broken by type name for
// determinism. Set Priority whenever two candidates can accept the same context; leaving such a pair
// unordered is CIALLO009, because then only the name decides which one wins.
//
// A tool NOT annotated here is manual: it declares its own [Substate] field on
// GlobalInteractiveScope and configures its own entry trigger. See GlobalInteractiveScope.
[AttributeUsage(AttributeTargets.Class)]
public sealed class RequestedByToolButtonAttribute(ToolButton.Type button) : Attribute
{
    public ToolButton.Type Button { get; } = button;

    public int Priority { get; init; }
}

// A tool scope whose availability depends on the working layers. Implementing this interface is the
// declaration; there is no companion attribute to keep in sync.
//
// The generator emits, for every implementor that is also [RequestedByToolButton]:
//   .PermitReentryIf(WorkingLayersChanged, layers => resolves back to this tool)
// so a working-layer change inside one tool re-runs OnDeactivated/OnActivated against the new
// layers. Without it Stateless keeps the scope active as common ancestor and only the leaf
// interaction re-enters, leaving layer-bound state (ArrangementManager, BodyHolder.ProcessMode)
// pointing at the previous layer.
//
// CanHandleLayers is called only with a non-empty snapshot of live non-document layers, so it needs
// no null, liveness or document checks. It must stay a pure function of its argument: it is called
// before the transition, when InteractionManager.WorkingLayers still holds the OLD snapshot.
public interface ILayerDependent
{
    static abstract bool CanHandleLayers(ImmutableArray<Entity> layers);
}

// Access to another registered state without parent-child relationship.
// Annotated member must be writable by machine owner's bootstrap.
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class StateAccessAttribute : Attribute { }

// Declares direct child and assigns reference. [StateAccess] not needed.
// Integer is priority, not position; equal priorities preserve declaration order.
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class SubstateAttribute : Attribute
{
    public SubstateAttribute(int order = 0)
    {
        Order = order;
    }

    public int Order { get; }
}

public static class InteractionStateMachineExtensions
{
    public static StateMachine.StateConfiguration InitialTransitionDynamic(
        this StateMachine.StateConfiguration cfg,
        Func<InteractionState> destinationStateSelector)
    {
        var dummyTrigger = new Trigger("DummyInitialTransition");
        cfg.PermitDynamic(dummyTrigger, destinationStateSelector);
        cfg.OnEntry(() => InteractionManager.Fire(dummyTrigger));
        return cfg;
    }
}
