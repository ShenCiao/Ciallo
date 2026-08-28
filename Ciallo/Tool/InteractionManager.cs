using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Frent;
using Godot;
using Stateless;

namespace Ciallo.Tool;

using StateMachine = StateMachine<InteractionState, Trigger>;

public static partial class InteractionManager
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

    public static readonly GlobalInteractiveScope Global;
    public static readonly StateMachine StateMachine;
    public static readonly StateMachine.TriggerWithParameters<Entity, ImmutableArray<Entity>>
        DocumentOpened;
    public static readonly Trigger DocumentClosed = new("DocumentClosed");
    public static readonly StateMachine.TriggerWithParameters<ImmutableArray<Entity>>
        WorkingLayersChanged;
    public static readonly StateMachine.TriggerWithParameters<bool> TimelineRollingChanged;
    public static readonly StateMachine.TriggerWithParameters<ToolButton.Type?> ToolButtonSwitch;

    public static event Action StateChanged;

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
        // Generated construction creates one singleton per [RegisterState]. States remain rooted
        // by StateMachine. A normal Roslyn generator cannot splice statements into this method
        // body, so it emits InteractionStateGraph that this constructor calls.
        InteractionStateGraph.Create(out var global, out var noDocument);

        // Queued is Stateless's default; explicit for clarity.
        StateMachine = new(noDocument, FiringMode.Queued);
        Global = global;
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

        // After destination entry, including InitialTransition into the tool's hover interaction.
        // OnTransitioned is too early: destination is still the tool scope, so hover property
        // branches stay hidden until the next user interaction.
        StateMachine.OnTransitionCompleted(_ => StateChanged?.Invoke());

        InteractionStateGraph.Configure(StateMachine);
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
    // Scope/interaction OnExit can release document resources safely.
    public static void CloseDocument()
    {
        if (_document.IsNull) return;
        StateMachine.Fire(DocumentClosed);
    }

    // Only operation that requests WorkingLayers change. AutoloadTool forwards SelectionManager
    // stream; panels don't call independently. Empty, document-root and dead snapshots resolve to
    // unavailable at global scope; arity is each tool's own decision via ILayerDependent.
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
        AppPreference.PressedToolButton.Value = toolButton;
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

        return StateMachine.State is Interaction session && session.OnMouseButton(button, data);
    }

    public static bool DispatchKey(InputEventKey key)
    {
        // Interaction hotkeys used directly as triggers. Godot action matching has priority.
        foreach (var hotkey in RoutedInteractionHotkeys)
        {
            if (hotkey.IsPressedBy(key) && TryFire(Trigger.Press(hotkey))) return true;
            if (hotkey.IsReleasedBy(key) && TryFire(Trigger.Release(hotkey))) return true;
        }

        if (TryFire(Trigger.Get(key.Keycode, key.Pressed))) return true;

        return StateMachine.State is Interaction session && session.OnKey(key, _latestCursor);
        // Tool button shortcut handled by Godot gui system has lowest priority
    }

    public static void DispatchMotion(CursorMotionData data)
    {
        _latestCursor = data;
        if (StateMachine.State is not Interaction session)
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
    // CapturingInteraction must make this trigger fireable; missing configuration is a graph error.
    public static void CancelForInputCaptureLoss()
    {
        if (StateMachine.State is CapturingInteraction && !TryFire(InputCaptureLost))
        {
            throw new InvalidOperationException(
                "Every CapturingInteraction must handle InputCaptureLost as Cancel.");
        }
    }

    // One canonical trigger per event. Source interaction decides Cancel vs End via CancelsOn.
    private static bool TryFire(Trigger trigger)
    {
        if (!StateMachine.CanFire(trigger)) return false;
        StateMachine.Fire(trigger);
        return true;
    }

}

[RegisterState]
public sealed class NoDocument : InteractionState { }

[RegisterState]
public sealed class TimelineRolling : InteractionState { }