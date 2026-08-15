using Ciallo.Data;
using Ciallo.GuiControl;
using Frent;
using Godot;
using Stateless;
using Microsoft.Extensions.DependencyInjection;
using System;
using Ciallo.Widget;

namespace Ciallo.Tool;

using StateMachine = StateMachine<IInteractionState, ToolBase.Trigger>;

public interface IInteractionState
{
    void OnEntry(StateMachine.Transition transition);
    void OnExit(StateMachine.Transition transition);

    void BeforeTransitionSrcEnd(IInteractionState src) { }
}

// For non leafs in state tree
// Stateless transition within a state's substates does not trigger the state's OnEntry&OnExit.
// Super state creating guideline: should be created by needs for sharing input handling/object lifecycle, not by actual GUI buttons
// e.g. Select tool for different layers having the shared tool button,
// but do not share anything initialized/destroyed in OnEntry or OnExit, so no superstate for them.
//
// State hierarchy declaration convention:
// - A scope's primary constructor is its complete DI constructor.
// - Parameters marked with [Substate] are the scope's direct substates.
// - Unmarked parameters are ordinary services, helpers, or cross-scope state references.
// - The source generator emits SubstateOf relationships only for [Substate] parameters.
//
// This keeps direct children visible in the scope declaration without overloading constructor
// selection semantics. ConfigureStateMachine can focus on transitions, while Microsoft DI sees
// one ordinary constructor containing all dependencies.
public interface IInteractiveScope : IInteractionState
{
    void ConfigureStateMachine(StateMachine stateMachine);
}

// Base for leafs in state tree
public abstract class InteractiveSession : IInteractionState
{
    public Entity Document => AppDocumentManager.WorkingDocument.Value;

    public virtual void BeforeTransitionSrcEnd(IInteractionState src) { }
    public abstract void Start(CursorButtonData data);
    public abstract void Moving(CursorMotionData data);
    public abstract void End(CursorButtonData data);
    public abstract void Cancel();
    public abstract bool OnKey(InputEventKey key, CursorButtonData data);
    public virtual bool OnMouseButton(InputEventMouseButton button, CursorButtonData data) => false;

    public void OnEntry(StateMachine.Transition transition)
    {
        var cursorMotion = Document.Get<WorldEventDispatcher>().CurrentCursorMotion;
        Start(cursorMotion);
    }

    public void OnExit(StateMachine.Transition transition)
    {
        if (transition.Destination is InteractiveSession dst && ReferenceEquals(transition.Source, this))
        {
            dst.BeforeTransitionSrcEnd(transition.Source);
        }

        var trigger = transition.Trigger;

        if (trigger == ToolBase.Trigger.Get(AppHotkeys.Global.InteractionCancel, true) ||
            trigger == ToolBase.Trigger.Get(AppHotkeys.Global.InteractionCancel, false))
        // Tool switch trigger also as cancel)
        {
            Cancel();
        }
        else
        {
            var cursorMotion = Document.Get<WorldEventDispatcher>().CurrentCursorMotion;
            End(cursorMotion);
        }
    }
}

public static partial class InteractionManager
{
    public static StateMachine StateMachine;

    static InteractionManager()
    {
        var s = new ServiceCollection();
        // Note: Should add a "Inactive" (Document is not loaded state by default)
        // StateMachine = new(Inactive);
        // Generated from [RegisterService].
        s.AddSingleton<GlobalInteractiveScope>();
        s.AddSingleton<SelectImageTool>();
        s.AddSingleton<SelectImageHover>();
        s.AddSingleton<SelectImageInteractor>();
        // and others..

        var provider = s.BuildServiceProvider();

        // Generated lifecycle registration.
        StateMachine.Configure(provider.GetService<GlobalInteractiveScope>())
            .OnEntry(t => provider.GetService<GlobalInteractiveScope>().OnEntry(t))
            .OnExit(t => provider.GetService<GlobalInteractiveScope>().OnExit(t));
        // and others...

        // Generated hierarchy. Only parameters marked with [Substate] create SubstateOf relationships.
        // The generator should report errors for invalid [Substate] parameter types, orphan states,
        // duplicate parents, self-parenting, and cycles in the generated hierarchy.
        StateMachine.Configure(provider.GetService<SelectImageTool>())
            .SubstateOf(provider.GetService<GlobalInteractiveScope>());
        StateMachine.Configure(provider.GetService<SelectImageHover>())
            .SubstateOf(provider.GetService<SelectImageTool>());
        StateMachine.Configure(provider.GetService<SelectImageInteractor>())
            .SubstateOf(provider.GetService<SelectImageTool>());
        // and others...
    }
}


[AttributeUsage(AttributeTargets.Class)]
public class RegisterServiceAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Parameter)]
public sealed class SubstateAttribute : Attribute { }

[RegisterService]
public partial class GlobalInteractiveScope : IInteractiveScope
{
    public void ConfigureStateMachine(StateMachine sm)
    {
        sm.Configure(this)
            .Permit(ToolBase.Trigger.Press(AppHotkeys.Global.ToolSelection), selectImage);
    }

    public void OnEntry(StateMachine<IInteractionState, ToolBase.Trigger>.Transition transition)
    {
    }

    public void OnExit(StateMachine<IInteractionState, ToolBase.Trigger>.Transition transition)
    {
    }
}

// Only marked parameters are direct substates. Unmarked parameters remain ordinary DI dependencies.
public partial class GlobalInteractiveScope(
    [Substate] SelectImageTool selectImage
);

[RegisterService]
public class SelectImageTool(
    [Substate] SelectImageHover hover,
    [Substate] SelectImageInteractor left
) : IInteractiveScope
{
    public void ConfigureStateMachine(StateMachine sm)
    {
        sm.Configure(this).InitialTransition(hover);
        sm.Configure(hover)
            .Permit(ToolBase.Trigger.Press(MouseButton.Left), left);
        sm.Configure(left)
            .Permit(ToolBase.Trigger.Release(MouseButton.Left), hover)
            .Permit(ToolBase.Trigger.Press(AppHotkeys.Global.InteractionCancel), hover);
    }

    public void OnEntry(StateMachine.Transition transition)
    {
        throw new NotImplementedException();
    }

    public void OnExit(StateMachine.Transition transition)
    {
        throw new NotImplementedException();
    }
}


[RegisterService]
public class SelectImageHover : InteractiveSession
{
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
}

[RegisterService]
public class SelectImageInteractor : InteractiveSession
{
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
}

// Use state hierarchy to draw property. States implement this interface
// To register, call state's DrawProperty foreach 
public interface IToolPropertyProvider
{
    void DrawProperty(PropertyContainer container, Control substatesControl /*Could be null */)
    {
        // Draw GUI before substates GUI
        // container.AddChild(OwnControlNodesBefore);
        // ...
        container.AddChild(substatesControl);
        // Draw GUI after substates GUI ...
    }
}