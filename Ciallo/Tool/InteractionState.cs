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

// Scopes are process-lifetime singletons, so they hold no per-document state of their own: anything
// bound to a document or layer is loaded from Document/WorkingLayers in OnActivated, kept in instance
// fields, and released in OnDeactivated.
public abstract class InteractionScope : InteractionState
{
    public abstract void ConfigureStateMachine(StateMachine stateMachine);

    protected virtual void OnActivated() { }
    protected virtual void OnDeactivated() { }

    public sealed override void OnEntry(StateMachine.Transition transition) => OnActivated();
    public sealed override void OnExit(StateMachine.Transition transition) => OnDeactivated();
}

// One continuous stretch of user input, from entry to End or Cancel. An interaction publishes semantic
// events with Fire(Trigger) but never drives the machine itself.
public abstract class Interaction : InteractionState
{
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

    // Called on the DESTINATION before the source exits, so transient source state can still be read.
    // Runs ahead of the source's End/Cancel either way. `source == this` is a reentry.
    // Used to carry accumulated stroke data across a paint-interaction switch without lifting the pen.
    public virtual void BeforeSourceExit(Interaction source) { }

    // Whether leaving via this transition should Cancel instead of End; the exiting interaction decides.
    // Override when a transition means abandonment rather than completion — see CapturingInteraction.
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

        if (CancelsOn(transition))
        {
            Cancel();
            return;
        }

        End(InteractionManager.LatestCursor);
    }
}

// Consumes all input by default; override OnKey/OnMouseButton to act on specific events, returning
// true to keep consuming. Cancels on every trigger that costs it the context it was working against.
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

// Required on every state class: the generator only sees states carrying this.
[AttributeUsage(AttributeTargets.Class)]
public sealed class RegisterStateAttribute : Attribute { }

// Makes this tool reachable from a tool-panel button. The generator wires it as a substate of
// GlobalInteractiveScope and adds it to the button resolver, so an annotated tool needs no field there.
//
// Several tools may share one button; they are expected to accept disjoint layer contexts, so at most
// one answers and the order between them is not observable. There is no priority to declare — if two
// ever do accept the same layers, a DEBUG assertion in the generated resolver names both.
//
// Without this attribute a tool is manual: declare a [Substate] field on GlobalInteractiveScope and
// configure its own entry trigger there. That is the path for a tool no button reaches.
[AttributeUsage(AttributeTargets.Class)]
public sealed class RequestedByToolButtonAttribute(ToolButton.Type button) : Attribute
{
    public ToolButton.Type Button { get; } = button;
}

// Implement on a tool whose availability depends on the working layers. Implementing it is the whole
// declaration — there is no attribute to keep in sync.
//
// Besides answering the button, this makes the generator emit a guarded reentry on WorkingLayersChanged
// for the tool. Without it Stateless would keep the scope active as common ancestor and re-enter only
// the leaf interaction, leaving layer-bound state (ArrangementManager, BodyHolder.ProcessMode) pointing
// at the previous layer.
//
// CanHandleLayers only ever receives a non-empty snapshot of live non-document layers, so skip null,
// liveness and document checks. Decide from the argument alone: at call time
// InteractionManager.WorkingLayers still holds the pre-transition snapshot.
public interface ILayerDependent
{
    static abstract bool CanHandleLayers(ImmutableArray<Entity> layers);
}

// Reference another registered state without implying a parent-child relationship.
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class StateAccessAttribute : Attribute { }

// Declares a direct child and assigns the reference; [StateAccess] is then unnecessary. Order affects
// only sibling sequence, not nesting; equal orders keep declaration order.
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
