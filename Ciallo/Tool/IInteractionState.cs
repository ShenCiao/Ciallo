using Ciallo.Data;
using Ciallo.GuiControl;
using Ciallo.Misc;
using Frent;
using Godot;
using Stateless;
using System;
using System.Collections.Generic;
using Ciallo.Widget;
using System.Collections.Immutable;
using System.Linq;
using R3;

namespace Ciallo.Prototype;

using StateMachine = StateMachine<InteractionState, Trigger>;

// A trigger identifies only what happened. The exiting InteractiveSession decides whether to
// commit (End) or rollback (Cancel) through CancelsOn(transition). The same input can mean
// different things to different sessions without configuration conflict.
//
// Events that don't cause a transition are routed to the session's OnKey/OnMouseButton handlers.
public class Trigger
{
    public string Name { get; }

    public Trigger(string name)
    {
        Name = name;
    }

    // "The data this state read is stale; read it again." Published on undo/redo, selection changes.
    // Only states that explicitly permit reentry react; others ignore it.
    public static readonly Trigger Refresh = new("Refresh");

    // Canonical instances, so reference equality is a complete trigger comparison.
    private static readonly Dictionary<MouseButton, Trigger> MouseButtonPress = new();
    private static readonly Dictionary<MouseButton, Trigger> MouseButtonRelease = new();
    private static readonly Dictionary<Key, Trigger> KeyPress = new();
    private static readonly Dictionary<Key, Trigger> KeyRelease = new();
    private static readonly Dictionary<Hotkey, Trigger> HotkeyPress = new();
    private static readonly Dictionary<Hotkey, Trigger> HotkeyRelease = new();

    public static Trigger Get(MouseButton button, bool isPress)
    {
        var dict = isPress ? MouseButtonPress : MouseButtonRelease;
        if (!dict.TryGetValue(button, out var trigger))
        {
            trigger = new Trigger($"{(isPress ? "Press" : "Release")}({button})");
            dict[button] = trigger;
        }
        return trigger;
    }

    public static Trigger Get(Key key, bool isPress)
    {
        var dict = isPress ? KeyPress : KeyRelease;
        if (!dict.TryGetValue(key, out var trigger))
        {
            trigger = new Trigger($"{(isPress ? "Press" : "Release")}({key})");
            dict[key] = trigger;
        }
        return trigger;
    }

    public static Trigger Get(Hotkey hotkey, bool isPress)
    {
        var dict = isPress ? HotkeyPress : HotkeyRelease;
        if (!dict.TryGetValue(hotkey, out var trigger))
        {
            trigger = new Trigger($"{(isPress ? "Press" : "Release")}({hotkey.Name})");
            dict[hotkey] = trigger;
        }
        return trigger;
    }

    public static Trigger Press(MouseButton button) => Get(button, true);
    public static Trigger Release(MouseButton button) => Get(button, false);
    public static Trigger Press(Key key) => Get(key, true);
    public static Trigger Release(Key key) => Get(key, false);
    public static Trigger Press(Hotkey hotkey) => Get(hotkey, true);
    public static Trigger Release(Hotkey hotkey) => Get(hotkey, false);
}

public abstract class InteractionState
{
    // Manager-owned snapshots. Sessions can trust these are valid (no null/availability checks needed).
    protected static Entity Document => InteractionManager.Document;
    protected ImmutableArray<Entity> WorkingLayers => InteractionManager.WorkingLayers;
    protected Entity WorkingLayer => WorkingLayers.Single();

    // Publish semantic events. Queued: defers during active transitions, otherwise synchronous.
    // Never Fire from OnExit (would resolve from destination state). Use BeforeSourceExit instead.
    protected static void Fire(Trigger trigger) => InteractionManager.Fire(trigger);

    public virtual void OnEntry(StateMachine.Transition transition) { }
    public virtual void OnExit(StateMachine.Transition transition) { }
}

// Base for non-leaf states in the hierarchy. Super states share input handling or object lifecycle.
// State hierarchy is declared via [Substate] attributes, not constructor parameters.
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

// Base for leaf states: interactive sessions that handle user input.
// Sessions publish semantic events via Fire(Trigger) but never own the machine.
// All sessions must support InteractionConfirm hotkey to commit and return to hover state.
public abstract class InteractiveSession : InteractionState
{
    // Per-session throttling config. Read once per motion event; override the property, don't assign.
    public virtual TimeSpan MovingMinInterval => TimeSpan.FromMilliseconds(5);

    // Replace the `BeforeTransitionSrcEnd` in lagacy code.
    // Called on destination before source exits. Allows reading transient source state before teardown.
    // Source == this means reentry. Runs before CancelsOn/End/Cancel regardless of disposition.
    // Current use case: transfer accumulated stroke data when switching paint sessions without lifting pen.
    public virtual void BeforeSourceExit(InteractiveSession source) { }

    // The exiting session decides whether this transition cancels. Transitions to scopes/sentinels
    // (not sessions) are decided by trigger identity. Hover sessions whose End is aliased to Cancel
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
        if (transition.Destination is InteractiveSession destination &&
            ReferenceEquals(transition.Source, this))
        {
            destination.BeforeSourceExit(this);
        }

        // Refresh and normal completion both use End before destination entry. ActiveInteraction
        // should not permit Refresh unless normal End is valid.
        if (CancelsOn(transition))
        {
            Cancel();
            return;
        }

        End(InteractionManager.LatestCursor);
    }
}

// Active interactions consume all input by default. Override OnKey/OnMouseButton to process events.
// Default cancellation: loses context (cancel key, capture loss, document close, timeline rolling,
// tool switch, layer change). Override CancelsOn for different behavior.
// All ActiveInteraction sessions must permit InteractionConfirm to commit and return to hover.
public abstract class ActiveInteraction : InteractiveSession
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

/*
 * Tool system refactoring plan
 * ==============================
 * Core goal: Centralize all interactive sessions into one state machine while preserving existing
 * tool behavior.
 *
 * Key design decisions:
 * - One process-lifetime InteractionManager owns the StateMachine
 * - Triggers identify inputs; sessions decide Cancel vs End via CancelsOn(transition)
 * - ActiveInteraction cancels on context loss (cancel key, tool switch, layer change);
 *   commits on explicit completion (Release, Confirm, custom events like PaintEnd)
 * - State hierarchy declared via [Substate] and [StateAccess] attributes
 * - Tool/layer routing: GlobalInteractiveScope switch examines WorkingLayers snapshot
 * - Queued firing: nested Fire defers until current transition completes
 * - ToolButton.Type? persists user selection; null is valid "no latched tool" (pan/zoom only mode)
 * - Trigger.Refresh is the single "data stale" event from undo/redo/selection changes
 *
 * State hierarchy attributes:
 * - [Substate]: declares child + assigns reference (implies access)
 * - [StateAccess]: access-only reference without parent-child relationship
 * - [Substate(order)]: explicit priority for sibling ordering
 *
 * Per-document state pattern:
 * - Tool scopes are process-lifetime singletons
 * - Load per-document state from Document/WorkingLayers in OnActivated, store in instance fields
 * - Reentry on WorkingLayersChanged calls OnDeactivated then OnActivated to refresh context
 * - Example: PaintStrokeTool loads Arrangement from WorkingLayer.Get<ArrangementManager>()
 *
 * PermitReentryIf behavior:
 * - When guard succeeds: exits active child and scope, then enters scope and initial child
 * - When guard fails: falls through to parent's PermitDynamic for the same trigger
 * - GlobalInteractiveScope.ResolveToolButton handles WorkingLayersChanged for all tools
 *
 * Confirm handling:
 * - All ActiveInteraction sessions must support InteractionConfirm to commit and return to hover
 * - Configure: .Permit(InteractionManager.ConfirmRequested, hoverState)
 * - Current tools missing Confirm: PaintStrokeTool, TrimTool (forgotten during prototyping)
 *
 * Special cases:
 * - GapBridgeTool uses InternalTransition for direct click actions without state change
 * - PaintStrokeInteractor completes on custom PaintEnd event, not Release(Left)
 *
 * Resource lifetime:
 * - InteractionPropertyTree lifetime tied to document; relies on GC + Godot QueueFree on close
 * - State constructors must not access InteractionManager static fields (construction before StateMachine)
 *
 * Migration checklist:
 * 1. Add InteractionConfirm to RoutedInteractionHotkeys
 * 2. Migrate canvas input routing to InteractionManager
 * 3. Migrate ToolButtonPanel, ToolPropertyPanel, AppDocumentManager
 * 4. Delete ToolManager, ITool, ToolBase, old Trigger, RegisterToolGenerator
 */

// Generated registration statements live inline in the owner of StateMachine. That is the fixed
// design: initialization order and the complete graph are visible in one place, and the emitted
// statements stay ordinary C# that can be read and edited like any other code. A normal Roslyn
// source generator cannot splice statements into an existing method body, so production generation
// emits this owner bootstrap as a whole, or emits one generated entry method on a partial
// InteractionManager that the authored constructor calls. That compiler limitation is independent of
// the state model and does not justify partial state classes.
public static class InteractionManager
{
    private static readonly Trigger DocumentOpenedTrigger = new("DocumentOpened");
    private static readonly Trigger WorkingLayersChangedTrigger = new("WorkingLayersChanged");
    private static readonly Trigger TimelineRollingChangedTrigger = new("TimelineRollingChanged");
    private static readonly Trigger ToolButtonSwitchTrigger = new("ToolButtonSwitch");

    // Tool-button hotkeys route to RequestTool before raw interaction hotkeys. This index contains
    // hotkeys used directly as state-machine triggers: Cancel and Confirm.
    private static readonly ImmutableArray<Hotkey> RoutedInteractionHotkeys =
        [AppHotkeys.Global.InteractionCancel, AppHotkeys.Global.InteractionConfirm];

    private static Entity _document = Entity.Null;
    private static ImmutableArray<Entity> _workingLayers = ImmutableArray<Entity>.Empty;
    private static bool _timelineRolling;
    private static CursorButtonData _latestCursor;
    private static CursorButtonData _lastDeliveredCursor;
    private static TimeSpan _accumulatedMotionInterval;

    public static readonly StateMachine StateMachine;
    public static readonly StateMachine.TriggerWithParameters<Entity, ImmutableArray<Entity>>
        DocumentOpened;
    public static readonly Trigger DocumentClosed = new("DocumentClosed");
    public static readonly StateMachine.TriggerWithParameters<ImmutableArray<Entity>>
        WorkingLayersChanged;
    public static readonly StateMachine.TriggerWithParameters<bool> TimelineRollingChanged;
    public static readonly StateMachine.TriggerWithParameters<ToolButton.Type?> ToolButtonSwitch;

    internal static Entity Document => _document;
    internal static ImmutableArray<Entity> WorkingLayers => _workingLayers;
    internal static CursorButtonData LatestCursor => _latestCursor;

    // Canonical trigger instances for reference equality comparison in CancelsOn.
    internal static readonly Trigger CancelRequested =
        Trigger.Press(AppHotkeys.Global.InteractionCancel);
    internal static readonly Trigger ConfirmRequested =
        Trigger.Press(AppHotkeys.Global.InteractionConfirm);
    internal static readonly Trigger InputCaptureLost = new("InputCaptureLost");

    static InteractionManager()
    {
        // Process-lifetime bootstrap, forced by AutoloadTool._Ready. Generated construction creates
        // one singleton per [RegisterState]. States remain rooted by StateMachine.
        var global = new GlobalInteractiveScope();
        var noDocument = new NoDocument();
        var timelineRolling = new TimelineRolling();
        var selectImage = new SelectImageTool();
        var selectImageHover = new SelectImageHover();
        var selectImageInteractor = new SelectImageInteractor();
        // and others..

        // Queued is Stateless's default; explicit for clarity.
        StateMachine = new(noDocument, FiringMode.Queued);
        DocumentOpened =
            StateMachine.SetTriggerParameters<Entity, ImmutableArray<Entity>>(
                DocumentOpenedTrigger);
        WorkingLayersChanged =
            StateMachine.SetTriggerParameters<ImmutableArray<Entity>>(
                WorkingLayersChangedTrigger);
        TimelineRollingChanged =
            StateMachine.SetTriggerParameters<bool>(
                TimelineRollingChangedTrigger);
        ToolButtonSwitch =
            StateMachine.SetTriggerParameters<ToolButton.Type?>(
                ToolButtonSwitchTrigger);

        // OnTransitioned runs after source exits, before destination enters. Exits observe old
        // snapshots; entries observe new ones. Initial transitions may report same trigger again.
        StateMachine.OnTransitioned(transition =>
        {
            if (transition.Trigger == DocumentOpened.Trigger)
            {
                _document = (Entity)transition.Parameters[0];
                _workingLayers = (ImmutableArray<Entity>)transition.Parameters[1];
                _timelineRolling = false;
                _latestCursor = default;
            }
            else if (transition.Trigger == DocumentClosed)
            {
                _document = Entity.Null;
                _workingLayers = ImmutableArray<Entity>.Empty;
                _timelineRolling = false;
                _latestCursor = default;
            }
            else if (transition.Trigger == WorkingLayersChanged.Trigger)
            {
                _workingLayers = (ImmutableArray<Entity>)transition.Parameters[0];
            }
            else if (transition.Trigger == TimelineRollingChanged.Trigger)
            {
                _timelineRolling = (bool)transition.Parameters[0];
            }

            _accumulatedMotionInterval = TimeSpan.Zero;
            _lastDeliveredCursor = _latestCursor;
        });

        // Generated reference wiring for [Substate] and [StateAccess]. [Substate] also produces
        // SubstateOf calls below; [StateAccess] produces only the assignment.
        global._noDocument = noDocument;
        global._timelineRolling = timelineRolling;
        global._selectImage = selectImage;
        selectImage._hover = selectImageHover;
        selectImage._left = selectImageInteractor;
        selectImageHover.Tool = selectImage;
        selectImageInteractor.Tool = selectImage;

        // Generated lifecycle registration. Reference wiring completes before any ConfigureStateMachine.
        InteractionState[] states =
            [global, noDocument, timelineRolling, selectImage, selectImageHover, selectImageInteractor];
        foreach (var state in states)
        {
            StateMachine.Configure(state)
                .OnEntry(state.OnEntry)
                .OnExit(state.OnExit);
        }

        // Generated hierarchy. Emit SubstateOf only for [Substate] members. Siblings ordered by
        // Substate.Order, then declaration order.
        StateMachine.Configure(noDocument).SubstateOf(global);
        StateMachine.Configure(timelineRolling).SubstateOf(global);
        StateMachine.Configure(selectImage).SubstateOf(global);
        StateMachine.Configure(selectImageHover).SubstateOf(selectImage);
        StateMachine.Configure(selectImageInteractor).SubstateOf(selectImage);

        foreach (var scope in states.OfType<InteractionScope>())
        {
            scope.ConfigureStateMachine(StateMachine);
        }

        // Finalize after all Configure/SubstateOf. Enables GetSubstates extension.
        StateMachine.BuildSubstateMap();
    }

    // AutoloadTool calls this in _Ready to force static bootstrap. The empty body is intentional.
    public static void Initialize() { }

    // Internal state-to-machine publication. Invalid events throw Stateless's unhandled exception.
    internal static void Fire(Trigger trigger) => StateMachine.Fire(trigger);

    // One application-wide machine. Opening supplies first context; closing returns to NoDocument.
    // Tool modes and settings persist across this boundary (singleton scopes, not per-document).
    public static void OpenDocument(Entity document, ImmutableArray<Entity> workingLayers)
    {
        StateMachine.Fire(DocumentOpened, document, workingLayers);
    }

    // Called from AppDocumentManager.Remove while document is still WorkingDocument and World alive.
    // Scope/session OnExit can release document resources safely.
    public static void CloseDocument()
    {
        StateMachine.Fire(DocumentClosed);
    }

    // Only operation that requests WorkingLayers change. AutoloadTool forwards SelectionManager
    // stream; panels don't call independently. Empty/document-root/dead/multi-layer snapshots
    // resolve to unavailable at global scope.
    public static void ChangeWorkingLayers(ImmutableArray<Entity> nextLayers)
    {
        StateMachine.Fire(WorkingLayersChanged, nextLayers);
    }

    // AutoloadTool forwards TimelineSetting.IsRollingFrame through DistinctUntilChanged.
    // Local mirror suppresses initial false emission. Snapshot updates in OnTransitioned.
    public static void SetTimelineRolling(bool rolling)
    {
        if (_timelineRolling == rolling) return;

        StateMachine.Fire(TimelineRollingChanged, rolling);
    }

    // CommandManager.HistoryNavigated subscription. Only states that explicitly permit reentry react.
    public static void NotifyRefresh()
    {
        TryFire(Trigger.Refresh);
    }

    // Selected button is app preference; StateMachine.State is concrete interaction for
    // document/layers. Updating preference first lets NoDocument/TimelineRolling ignore the
    // transition without a pending-tool field. Repeated request is consumed without reentry.
    public static bool RequestTool(ToolButton.Type? toolButton)
    {
        if (ToolButton.ActiveToolButton.Value == toolButton) return true;

        ToolButton.ActiveToolButton.Value = toolButton;
        StateMachine.Fire(ToolButtonSwitch, toolButton);
        return true;
    }

    // WorldEventDispatcher keeps touch on existing path. For keys/buttons it calls these, sets
    // viewport handled when true, and lets zoom/pan run only when !IsInputHandled().
    public static bool DispatchMouseButton(InputEventMouseButton button, CursorButtonData data)
    {
        _latestCursor = data;
        _lastDeliveredCursor = data;
        _accumulatedMotionInterval = TimeSpan.Zero;

        if (TryFire(Trigger.Get(button.ButtonIndex, button.Pressed))) return true;

        return StateMachine.State is InteractiveSession session && session.OnMouseButton(button, data);
    }

    public static bool DispatchKey(InputEventKey key)
    {
        // Tool shortcuts have transition priority. WorldEventDispatcher marks handled before Godot
        // reaches shortcut-input phase.
        if (ToolButton.TryResolveHotkey(key, out var toolButton))
        {
            return RequestTool(toolButton);
        }

        // Interaction hotkeys used directly as triggers. Godot action matching has priority.
        foreach (var hotkey in RoutedInteractionHotkeys)
        {
            if (hotkey.IsPressedBy(key) && TryFire(Trigger.Press(hotkey))) return true;
            if (hotkey.IsReleasedBy(key) && TryFire(Trigger.Release(hotkey))) return true;
        }

        if (TryFire(Trigger.Get(key.Keycode, key.Pressed))) return true;

        return StateMachine.State is InteractiveSession session && session.OnKey(key, _latestCursor);
    }

    public static void DispatchMotion(CursorMotionData data)
    {
        _latestCursor = data;
        if (StateMachine.State is not InteractiveSession session)
        {
            _lastDeliveredCursor = data;
            _accumulatedMotionInterval = TimeSpan.Zero;
            return;
        }

        _accumulatedMotionInterval += data.TimeDelta;
        if (_accumulatedMotionInterval <= session.MovingMinInterval) return;

        CursorMotionData motion = data;
        motion.ScreenDelta = data.ScreenPosition - _lastDeliveredCursor.ScreenPosition;
        motion.WorldDelta = data.WorldPosition - _lastDeliveredCursor.WorldPosition;
        motion.PressureDelta = data.Pressure - _lastDeliveredCursor.Pressure;
        motion.TiltDelta = data.Tilt - _lastDeliveredCursor.Tilt;
        motion.TimeDelta = _accumulatedMotionInterval;

        session.Moving(motion);
        _lastDeliveredCursor = data;
        _accumulatedMotionInterval = TimeSpan.Zero;
    }

    // Window focus loss and pointer capture loss use the same explicit cancellation trigger. Every
    // ActiveInteraction must make this trigger fireable; missing configuration is a graph error.
    public static void CancelForInputCaptureLoss()
    {
        if (StateMachine.State is ActiveInteraction && !TryFire(InputCaptureLost))
        {
            throw new InvalidOperationException(
                "Every ActiveInteraction must handle InputCaptureLost as Cancel.");
        }
    }

    // One canonical trigger per event. Source session decides Cancel vs End via CancelsOn.
    private static bool TryFire(Trigger trigger)
    {
        if (!StateMachine.CanFire(trigger)) return false;
        StateMachine.Fire(trigger);
        return true;
    }
}


// State hierarchy declaration attributes:
// - [RegisterState]: marks a state for registration in InteractionManager
// - [Substate]: declares child + assigns reference (implies access)
// - [StateAccess]: access-only reference without parent-child relationship
[AttributeUsage(AttributeTargets.Class)]
public sealed class RegisterStateAttribute : Attribute { }

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

[RegisterState]
public sealed class NoDocument : InteractionState { }

[RegisterState]
public sealed class TimelineRolling : InteractionState { }

[RegisterState]
public class GlobalInteractiveScope : InteractionScope
{
    [Substate]
    internal NoDocument _noDocument = null!;

    [Substate]
    internal TimelineRolling _timelineRolling = null!;

    [Substate]
    internal SelectImageTool _selectImage = null!;

    public override void ConfigureStateMachine(StateMachine sm)
    {
        sm.Configure(_noDocument)
            .Ignore(InteractionManager.ToolButtonSwitch.Trigger)
            .PermitDynamic(
                InteractionManager.DocumentOpened,
                (_, layers) => ResolveToolButton(ToolButton.ActiveToolButton.Value, layers));
        sm.Configure(_timelineRolling)
            // RequestTool persists selection. Reentry updates snapshot without lifecycle.
            .Ignore(InteractionManager.ToolButtonSwitch.Trigger)
            .PermitReentry(InteractionManager.WorkingLayersChanged.Trigger);
        sm.Configure(this)
            .Permit(InteractionManager.DocumentClosed, _noDocument)
            .PermitDynamic(
                InteractionManager.ToolButtonSwitch,
                toolButton => ResolveToolButton(toolButton, InteractionManager.WorkingLayers))
            // GlobalInteractiveScope handles WorkingLayersChanged for all tools via ResolveToolButton.
            // Concrete tool scopes can override with PermitReentryIf(WorkingLayersChanged, CanHandleLayers)
            // to refresh their context. When guard fails, falls through to this parent PermitDynamic.
            .PermitDynamic(
                InteractionManager.WorkingLayersChanged,
                layers => ResolveToolButton(ToolButton.ActiveToolButton.Value, layers))
            .PermitDynamic(
                InteractionManager.TimelineRollingChanged,
                rolling => rolling
                    ? _timelineRolling
                    : ResolveToolButton(
                        ToolButton.ActiveToolButton.Value,
                        InteractionManager.WorkingLayers));
    }

    // Concrete tool scope with per-layer routing. Returns concrete scope or this (unavailable).
    private InteractionState ResolveToolButton(
        ToolButton.Type? toolButton,
        ImmutableArray<Entity> layers)
    {
        if (toolButton is null || layers.Length != 1) return this;

        var layer = layers[0];
        if (layer.IsDyingOrDead || layer.IsDocument) return this;

        return toolButton switch
        {
            ToolButton.Type.Select when layer.Has<ImageLayerSetting>() => _selectImage,
            // e.g. Select + ShapeLayer/VectorFillLayer => _selectPolyline
            _ => this,
        };
    }
}

[RegisterState]
public class SelectImageTool : InteractionScope, IToolPropertyProvider
{
    [Substate]
    internal SelectImageHover _hover = null!;

    [Substate]
    internal SelectImageInteractor _left = null!;

    public override void ConfigureStateMachine(StateMachine sm)
    {
        // When guard succeeds, reentry exits active child and scope, then enters scope and initial
        // child. When guard fails, global transition resolves another scope.
        sm.Configure(this)
            .InitialTransition(_hover)
            .PermitReentryIf(InteractionManager.WorkingLayersChanged, CanHandleLayers);
        sm.Configure(_hover)
            .Permit(Trigger.Press(MouseButton.Left), _left)
            // Hover caches data at Start, so Refresh restarts it. Interactor deliberately doesn't
            // permit Refresh, so the same notification is no-op mid-gesture.
            .PermitReentry(Trigger.Refresh);
        sm.Configure(_left)
            // Same Release(Left) instance is interactor's completion. A sibling that wanted this
            // input to discard would say so in its own CancelsOn.
            .Permit(Trigger.Release(MouseButton.Left), _hover)
            .Permit(InteractionManager.CancelRequested, _hover)
            .Permit(InteractionManager.InputCaptureLost, _hover);
    }

    private static bool CanHandleLayers(ImmutableArray<Entity> layers)
    {
        return layers.Length == 1 &&
               !layers[0].IsDyingOrDead &&
               layers[0].Has<ImageLayerSetting>();
    }

    public void DrawPropertyBeforeSubstates(PropertyContainer container)
    {
        // Shared properties above substates. Values persist across documents (singleton state).
    }

    public void DrawPropertyAfterSubstates(PropertyContainer container)
    {
        // Scope commands below substates.
    }
}


[RegisterState]
public class SelectImageHover : InteractiveSession, IToolPropertyProvider
{
    // Access-only: Tool is owner/peer, not a child of hover state.
    [StateAccess]
    internal SelectImageTool Tool { get; set; } = null!;

    public override void Cancel()
    {
        throw new NotImplementedException();
    }

    public override void End(CursorButtonData data)
    {
        throw new NotImplementedException();
    }

    public override void Moving(CursorMotionData data)
    {
        throw new NotImplementedException();
    }

    public override bool OnKey(InputEventKey key, CursorButtonData data)
    {
        throw new NotImplementedException();
    }

    public override void Start(CursorButtonData data)
    {
        throw new NotImplementedException();
    }

    public void DrawPropertyBeforeSubstates(PropertyContainer container)
    {
        // Leaf-only properties. States create but don't retain document-specific controls.
    }
}

[RegisterState]
public class SelectImageInteractor : ActiveInteraction
{
    [StateAccess]
    internal SelectImageTool Tool { get; set; } = null!;

    public override void Cancel()
    {
        throw new NotImplementedException();
    }

    public override void End(CursorButtonData data)
    {
        throw new NotImplementedException();
    }

    public override void Moving(CursorMotionData data)
    {
        throw new NotImplementedException();
    }

    public override void Start(CursorButtonData data)
    {
        throw new NotImplementedException();
    }
}

// Hierarchical property composition around direct substates.
// Providers are singletons: create controls here, never retain them.
public interface IToolPropertyProvider
{
    void DrawPropertyBeforeSubstates(PropertyContainer container) { }

    void DrawPropertyAfterSubstates(PropertyContainer container) { }
}

// State-machine hierarchy projected to Godot controls. ToolPropertyPanel owns this, builds once
// per document, and keeps one transition listener that calls RefreshVisibility.
public sealed class InteractionPropertyTree
{
    private readonly StateMachine _stateMachine;
    private readonly Dictionary<InteractionState, PropertyContainer> _stateBranches =
        new(ReferenceEqualityComparer.Instance);

    public PropertyContainer RootControl { get; }

    private InteractionPropertyTree(
        StateMachine stateMachine,
        InteractionState rootState,
        Entity document)
    {
        _stateMachine = stateMachine;
        RootControl = BuildBranch(rootState, document);
        RefreshVisibility();
    }

    public static InteractionPropertyTree Build(
        StateMachine stateMachine,
        InteractionState rootState,
        Entity document)
    {
        return new(stateMachine, rootState, document);
    }

    // Stateless reports true for leaf and superstates. Parent scope properties stay visible while
    // exactly the active child branch is shown.
    public void RefreshVisibility()
    {
        foreach (var (state, branch) in _stateBranches)
        {
            branch.Visible = _stateMachine.IsInState(state);
        }
    }

    private PropertyContainer BuildBranch(
        InteractionState state,
        Entity document)
    {
        var branch = new PropertyContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        _stateBranches.Add(state, branch);

        var provider = state as IToolPropertyProvider;
        provider?.DrawPropertyBeforeSubstates(branch);

        // Create hierarchy slot. Hide empty leaf slot to avoid layout spacing while keeping
        // recursive algorithm identical for every state.
        var substatesContainer = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        foreach (var substate in _stateMachine.GetSubstates(state))
        {
            substatesContainer.AddChild(BuildBranch(substate, document));
        }
        substatesContainer.Visible = substatesContainer.GetChildCount() > 0;
        branch.AddChild(substatesContainer);

        provider?.DrawPropertyAfterSubstates(branch);
        return branch;
    }
}

// Application-level tool-button catalog. Source generator reads enum declaration order, emits
// Definitions/GetDefinition/TryResolveHotkey, and reports errors.
public static partial class ToolButton
{
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class DefinitionAttribute(
        string iconPath,
        string tooltip) : Attribute
    {
        public string IconPath { get; } = iconPath;
        public string Tooltip { get; } = tooltip;

        // nameof keeps AppHotkeys member compile-checked; generator emits strongly typed access.
        public string ShortcutMember { get; set; }
    }

    public enum Type
    {
        // Stable persistence identifiers, independent of declaration order. Keep 0 undefined so
        // default(Type) cannot silently mean a real button.
        [Definition(
            "res://Icon/cursor-default-outline.svg",
            "Selection",
            ShortcutMember = nameof(AppHotkeys.Global.ToolSelection))]
        Select = 1,

        [Definition(
            "res://Icon/brush.svg",
            "Paint Brush",
            ShortcutMember = nameof(AppHotkeys.Global.ToolPaintBrush))]
        PaintStroke = 2,

        [Definition(
            "res://Icon/lasso-fill.svg",
            "Paint Fill",
            ShortcutMember = nameof(AppHotkeys.Global.ToolPaintFill))]
        PaintFill = 3,

        [Definition("res://Icon/bucket-fill-marker.svg", "Vector Fill")]
        VectorFill = 4,

        [Definition("res://Icon/water-drop.svg", "Liquify")]
        Liquify = 5,

        [Definition("res://Icon/scissor.svg", "Trim")]
        Trim = 6,

        [Definition("res://Icon/wrench.svg", "Gap Bridge")]
        GapBridge = 7,
    }

    // A button without ShortcutMember has no Hotkey (nullable). TryResolveHotkey skips it.
#nullable enable annotations
    public sealed record Descriptor(
        Type Type,
        string IconPath,
        string Tooltip,
        Hotkey? Shortcut);
#nullable restore annotations

    // Generated. Array order follows enum declaration order, not numeric order. ToolButtonPanel
    // creates one grouped toggle per descriptor.
    public static ImmutableArray<Descriptor> Definitions { get; } =
    [
        new(
            Type.Select,
            "res://Icon/cursor-default-outline.svg",
            "Selection",
            AppHotkeys.Global.ToolSelection),
        new(
            Type.PaintStroke,
            "res://Icon/brush.svg",
            "Paint Brush",
            AppHotkeys.Global.ToolPaintBrush),
        new(
            Type.PaintFill,
            "res://Icon/lasso-fill.svg",
            "Paint Fill",
            AppHotkeys.Global.ToolPaintFill),
        new(Type.VectorFill, "res://Icon/bucket-fill-marker.svg", "Vector Fill", null),
        new(Type.Liquify, "res://Icon/water-drop.svg", "Liquify", null),
        new(Type.Trim, "res://Icon/scissor.svg", "Trim", null),
        new(Type.GapBridge, "res://Icon/wrench.svg", "Gap Bridge", null),
    ];

    // Generated as a direct switch so callers neither reflect nor depend on enum numeric order.
    public static Descriptor GetDefinition(Type type) => type switch
    {
        Type.Select => Definitions[0],
        Type.PaintStroke => Definitions[1],
        Type.PaintFill => Definitions[2],
        Type.VectorFill => Definitions[3],
        Type.Liquify => Definitions[4],
        Type.Trim => Definitions[5],
        Type.GapBridge => Definitions[6],
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
    };

    // Generated from non-null ShortcutMember. InputMap matching preserves user-remappable Godot
    // action semantics.
    public static bool TryResolveHotkey(InputEventKey key, out Type type)
    {
        foreach (var definition in Definitions)
        {
            if (definition.Shortcut?.IsPressedBy(key) != true) continue;

            type = definition.Type;
            return true;
        }

        type = default;
        return false;
    }

    // User selection state. null is valid "no latched tool". Panel observes; RequestTool is sole writer.
    // Nullable type is resevered for future tools without a tool button (this will be light table related functionality), cannot be null now
    public static readonly ReactiveProperty<Type?> ActiveToolButton = new(Type.PaintStroke);
}
