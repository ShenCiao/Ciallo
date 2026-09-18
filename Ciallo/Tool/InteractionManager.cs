using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Frent;
using Godot;
using R3;
using Stateless;

namespace Ciallo.Tool;

using StateMachine = StateMachine<InteractionState, Trigger>;

public static partial class InteractionManager
{
    private static readonly Trigger DocumentOpenedTrigger = new("DocumentOpened");
    private static readonly Trigger SelectedLayersChangedTrigger = new("SelectedLayersChanged");
    private static readonly Trigger TimelineRollingChangedTrigger = new("TimelineRollingChanged");
    private static readonly Trigger ToolButtonSwitchTrigger = new("ToolButtonSwitch");

    private static readonly Dictionary<InteractionState, Trigger[]> InputRoutes;

    private static Entity _document = Entity.Null;
    private static ImmutableArray<Entity> _selectedLayers = ImmutableArray<Entity>.Empty;
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
        SelectedLayersChanged;
    public static readonly StateMachine.TriggerWithParameters<bool> TimelineRollingChanged;
    public static readonly StateMachine.TriggerWithParameters<ToolButton.Type?> ToolButtonSwitch;

    public static event Action StateChanged;

    internal static Entity Document => _document;
    internal static ImmutableArray<Entity> SelectedLayers => _selectedLayers;
    internal static CursorButtonData LatestCursor => _latestCursor;

    // This is the machine-level context boundary. Tool predicates only decide whether their own
    // scope fits; they must not also reimplement document/layer liveness checks.
    internal static bool HasUsableLayers(ImmutableArray<Entity> layers)
    {
        if (layers.IsEmpty) return false;

        foreach (var layer in layers)
        {
            if (layer.IsDyingOrDead || layer.IsDocument) return false;
        }

        return true;
    }

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
        SelectedLayersChanged =
            StateMachine.SetTriggerParameters<ImmutableArray<Entity>>(
                SelectedLayersChangedTrigger);
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
                _selectedLayers = (ImmutableArray<Entity>)transition.Parameters[1];
                _timelineRolling = false;
                _latestCursor = default;
            }
            else if (transition.Trigger == DocumentClosed)
            {
                _document = Entity.Null;
                _selectedLayers = ImmutableArray<Entity>.Empty;
                _timelineRolling = false;
                _latestCursor = default;
            }
            else if (transition.Trigger == SelectedLayersChanged.Trigger)
            {
                _selectedLayers = (ImmutableArray<Entity>)transition.Parameters[0];
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
        InputRoutes = StateMachine.BuildInputRoutes();

        // One process-lifetime subscription. Property panels only bind the shared selection.
        AppPreference.BucketFill.Mode.Skip(1).Subscribe(_ =>
        {
            if (ToolButton.ActiveToolButton.Value == ToolButton.Type.BucketFill)
            {
                // Re-resolve the selected button after a mode change. The same user-facing button
                // can map Marker output to either VectorFillTool or VectorFillLayerCreationTool.
                StateMachine.Fire(ToolButtonSwitch, ToolButton.Type.BucketFill);
            }
        });
    }

    // AutoloadTool calls this in _Ready to force static bootstrap. The empty body is intentional.
    public static void Initialize() { }

    // Internal state-to-machine publication. Invalid events throw Stateless's unhandled exception.
    internal static void Fire(Trigger trigger) => StateMachine.Fire(trigger);

    // One application-wide machine. Opening supplies first context; closing returns to NoDocument.
    // Tool modes and settings persist across this boundary (singleton scopes, not per-document).
    public static void OpenDocument(Entity document, ImmutableArray<Entity> selectedLayers)
    {
        StateMachine.Fire(DocumentOpened, document, selectedLayers);
    }

    // Called from AppDocumentManager.Remove while document is still WorkingDocument and World alive.
    // Scope/interaction OnExit can release document resources safely.
    public static void CloseDocument()
    {
        if (_document.IsNull) return;
        StateMachine.Fire(DocumentClosed);
    }

    // Only operation that requests SelectedLayers change. AutoloadTool forwards SelectionManager
    // stream; panels don't call independently. Empty, document-root and dead snapshots resolve to
    // unavailable at global scope; arity is each tool's own decision via ILayerDependent.
    public static void SetSelectedLayers(ImmutableArray<Entity> nextLayers)
    {
        StateMachine.Fire(SelectedLayersChanged, nextLayers);
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

        if (StateMachine.TryHandleInput(button, InputRoutes)) return true;

        return StateMachine.State is Interaction session && session.OnMouseButton(button, data);
    }

    public static bool DispatchKey(InputEventKey key)
    {
        if (StateMachine.TryHandleInput(key, InputRoutes)) return true;

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
