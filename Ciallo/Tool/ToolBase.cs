using System;
using System.Collections.Generic;
using System.Linq;
using Ciallo.Command;
using Ciallo.Widget;
using Frent;
using Godot;
using R3;
using Stateless;

namespace Ciallo.Tool;

using StateMachine = StateMachine<InteractiveSessionBase, ToolBase.Trigger>;
using StateConfiguration = StateMachine<InteractiveSessionBase, ToolBase.Trigger>.StateConfiguration;

/// <summary>
/// Create tool with state machine management.
/// </summary>
/// <remarks>
/// See stateless library https://github.com/dotnet-state-machine/stateless for the state machine configuration api.
/// </remarks>
/// <remarks>
/// Active interaction sessions consume key and mouse-button input by default.
/// </remarks>
/// <remarks>
/// Initial states are configured to refresh (call End then Start) when user undo/redo.
/// Key design idea: The only source of data change when hovering is undo/redo, so refreshing the session can reduce mind burden.
/// </remarks>
/// <remarks>
/// Prioity: State machine transit > Route to session
/// Prioity of trigger: Godot action > Key > Mouse button.
/// </remarks>
public abstract partial class ToolBase : ITool
{
    public readonly ReactiveProperty<InteractiveSessionBase> ActiveSession = new(ToolInactive.Instance);
    public readonly StateMachine Machine;

    public Entity Document
    {
        get;
        init
        {
            field = value;
            ConfigureStateMachine();
        }
    }
    public Entity[] WorkingLayers { get; set; }
    public Entity WorkingLayer => WorkingLayers.First();
    public SceneTree GetTree() => (SceneTree)Engine.GetMainLoop();

    private CursorButtonData _lastestCursor;
    private TimeSpan _accumulatedInterval = TimeSpan.Zero;

    private readonly HashSet<Hotkey> _triggerActions = [];

    private IDisposable _commandManagerSub;

    protected CursorButtonData LatestCursor => _lastestCursor;

    protected ToolBase()
    {
        Machine = new(() => ActiveSession.Value, s => ActiveSession.Value = s);

        Machine.Configure(ToolInactive.Instance)
            .Permit(Trigger.Activate, ToolActive.Instance);

        Machine.Configure(ToolActive.Instance)
            .Permit(Trigger.Deactivate, ToolInactive.Instance);
    }

    protected abstract void ConfigureStateMachine();

    public StateConfiguration Configure(InteractiveSessionBase session)
    {
        session.Tool = this;
        session.Document = Document;
        return Machine.Configure(session)
            .SubstateOf(ToolActive.Instance)
            .OnEntry(t =>
            {
                t.Destination.Start(_lastestCursor);
            })
            .OnExit(t =>
            {
                t.Destination.BeforeTransitionSrcEnd(t.Source);
                if (t.Trigger == Trigger.Get(AppHotkeys.Global.InteractionCancel, true) ||
                    t.Trigger == Trigger.Get(AppHotkeys.Global.InteractionCancel, false) ||
                    t.Trigger == Trigger.Deactivate)
                    t.Source.Cancel();
                else
                    t.Source.End(_lastestCursor);
            });
    }

    public StateConfiguration ConfigureInitial(InteractiveSessionBase session)
    {
        Machine.Configure(ToolActive.Instance)
            .InitialTransition(session);
        var cfg = Configure(session);
        cfg.PermitReentry(Trigger.Refresh);
        return cfg;
    }

    private IDisposable ObserveCommandManager(Entity document)
    {
        return document.Get<CommandManager>().HistoryNavigated.Subscribe(_ =>
        {
            if (Machine.CanFire(Trigger.Refresh))
                Machine.Fire(Trigger.Refresh);
        });
    }

    public virtual void OnActivated() { }
    public virtual void OnDeactivated() { }

    #region ITool

    public bool OnMouseButton(InputEventMouseButton button, CursorButtonData data)
    {
        _accumulatedInterval = TimeSpan.Zero;
        _lastestCursor = data;
        var trigger = Trigger.Get(button.ButtonIndex, button.Pressed);
        if (Machine.CanFire(trigger))
        {
            Machine.Fire(trigger);
            return true;
        }
        return Machine.State.OnMouseButton(button, data);
    }

    public bool OnKey(InputEventKey key)
    {
        // Check trigger action (dirty implementation)
        var actionTrigger = DetectTriggerAction(key);
        if (actionTrigger != null && Machine.CanFire(actionTrigger))
        {
            Machine.Fire(actionTrigger);
            return true;
        }
        // Check key
        var keyTrigger = Trigger.Get(key.Keycode, key.Pressed);
        if (Machine.CanFire(keyTrigger))
        {
            Machine.Fire(keyTrigger);
            return true;
        }
        // Route to current session
        return Machine.State.OnKey(key, _lastestCursor);
    }

    private Trigger DetectTriggerAction(InputEventKey key)
    {
        foreach (var action in _triggerActions)
        {
            if (action.IsPressedBy(key))
                return Trigger.Get(action, true);
            if (action.IsReleasedBy(key))
                return Trigger.Get(action, false);
        }
        return null;
    }

    public void OnMoving(CursorMotionData data)
    {
        _accumulatedInterval += data.TimeDelta;
        if (_accumulatedInterval > Machine.State.MovingMinInterval)
        {
            CursorMotionData motion = data; // Copy all non-delta
            motion.ScreenDelta = data.ScreenPosition - _lastestCursor.ScreenPosition;
            motion.WorldDelta = data.WorldPosition - _lastestCursor.WorldPosition;
            motion.PressureDelta = data.Pressure - _lastestCursor.Pressure;
            motion.TiltDelta = data.Tilt - _lastestCursor.Tilt;
            motion.TimeDelta = _accumulatedInterval;

            Machine.State.Moving(motion);
            _lastestCursor = data;
            _accumulatedInterval = TimeSpan.Zero;
        }
    }

    public virtual void DrawProperty(PropertyContainer container)
    {
        foreach (var state in Machine.GetInfo().States)
        {
            if (state.UnderlyingState is not InteractiveSessionBase session) continue;
            var sessionContainer = new PropertyContainer()
                .VisibleIf(ActiveSession, session)
                .AddToChildOf(container);
            session.DrawProperty(sessionContainer);
        }
    }

    public abstract bool CanHandleLayer(params Entity[] layerEs);

    public void OnActivate(params Entity[] layerEs)
    {
        foreach (var session in Machine.GetInfo().States.Select(info => (InteractiveSessionBase)info.UnderlyingState))
        {
            session.WorkingLayers = layerEs;
        }
        WorkingLayers = layerEs;
        OnActivated();
        Machine.Fire(Trigger.Activate);
        _commandManagerSub = ObserveCommandManager(Document);
    }

    public void OnDeactivate()
    {
        _commandManagerSub.Dispose();
        Machine.Fire(Trigger.Deactivate);
        OnDeactivated();
        WorkingLayers = null;
        foreach (var session in Machine.GetInfo().States.Select(info => (InteractiveSessionBase)info.UnderlyingState))
        {
            session.WorkingLayers = null;
        }
    }

    #endregion

    #region Triggers

    protected Trigger Press(MouseButton button) => Trigger.Get(button, true);
    protected Trigger Release(MouseButton button) => Trigger.Get(button, false);
    protected Trigger Press(Key key) => Trigger.Get(key, true);
    protected Trigger Release(Key key) => Trigger.Get(key, false);
    protected Trigger Press(Hotkey action)
    {
        _triggerActions.Add(action);
        return Trigger.Get(action, true);
    }
    protected Trigger Release(Hotkey action)
    {
        _triggerActions.Add(action);
        return Trigger.Get(action, false);
    }

    #endregion
}

public static class StateMachineExtension
{
    public static StateConfiguration InitialTransitionDynamic(this StateConfiguration cfg, Func<InteractiveSessionBase> destinationStateSelector)
    {
        var dummyTrigger = new ToolBase.Trigger("DummyInitialTransition");
        cfg.PermitDynamic(dummyTrigger, destinationStateSelector);
        cfg.OnEntry(() => cfg.Machine.Fire(dummyTrigger));
        return cfg;
    }
}

#region Internal Tool States

public abstract class InternalToolState : InteractiveSessionBase
{
    public override void Start(CursorButtonData cursor) { }
    public override void End(CursorButtonData cursor) { }
    public override void Cancel() { }
    public override bool OnKey(InputEventKey key, CursorButtonData data) { return false; }
    public override void Moving(CursorMotionData data) { }
}

public class ToolActive : InternalToolState
{
    public static readonly ToolActive Instance = new();
}

public class ToolInactive : InternalToolState
{
    public static readonly ToolInactive Instance = new();
}

#endregion
