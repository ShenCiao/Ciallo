using Ciallo.Data;
using Ciallo.GuiControl;
using Ciallo.Misc;
using Frent;
using Godot;
using Stateless;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
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
// - Marked parameter order is the stable order of direct substate property branches.
// - Unmarked parameters are ordinary services, helpers, or cross-scope state references.
// - The source generator emits SubstateOf relationships only for [Substate] parameters and
//   preserves parameter order when registering siblings.
//
// This keeps direct children visible in the scope declaration without overloading constructor
// selection semantics. ConfigureStateMachine can focus on transitions, while Microsoft DI sees
// one ordinary constructor containing all dependencies. The same hierarchy is also used to
// compose the normal tool-property GUI; a GUI-only grouping is not a reason to add a superstate.
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

        // Finalize once, after every generated Configure/SubstateOf call. Consumers can now use
        // the generic GetSubstates extension without owning another hierarchy representation.
        StateMachine.BuildSubstateMap();
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
) : IInteractiveScope, IToolPropertyProvider
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

    public void DrawPropertyBeforeSubstates(PropertyContainer container)
    {
        // Properties shared by Hover and Interactor go above the active session properties.
    }

    public void DrawPropertyAfterSubstates(PropertyContainer container)
    {
        // Scope commands that should follow the active session properties go here.
        // container.AddChild(CreateSelectionCommandButtons(document));
    }
}


[RegisterService]
public class SelectImageHover : InteractiveSession, IToolPropertyProvider
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

    public void DrawPropertyBeforeSubstates(PropertyContainer container)
    {
        // Leaf-only properties use the same hook. A leaf normally needs only the Before hook.
        // container.AddProperty("Hover option", CreateHoverOptionControl(document));
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

// A state's own properties are composed around its recursively-built direct substates.
// This is intentionally a hierarchical composition API, not an arbitrary GUI-sharing API.
// Reusable GUI shared by unrelated branches belongs to a separate property-block mechanism.
public interface IToolPropertyProvider
{
    // Called while ToolPropertyPanel builds controls for one specific WorkingDocument.
    // Providers are application-lifetime singletons: create controls here, but never retain them.
    void DrawPropertyBeforeSubstates(PropertyContainer container) { }

    void DrawPropertyAfterSubstates(PropertyContainer container) { }
}

// Projection of the state-machine hierarchy into Godot controls. Direct-substate indexing is a
// generic Stateless extension and is finalized independently with BuildSubstateMap.
//
// ToolPropertyPanel owns this object. It rebuilds the whole tree when WorkingDocument changes,
// QueueFrees the previous RootControl, and keeps one application-lifetime state-transition
// listener that calls RefreshVisibility on the current tree. This object deliberately does not
// subscribe to StateMachine itself, so rebuilding a document cannot leak transition callbacks.
public sealed class InteractionPropertyTree
{
    private readonly StateMachine _stateMachine;
    private readonly Dictionary<IInteractionState, PropertyContainer> _stateBranches =
        new(ReferenceEqualityComparer.Instance);

    public PropertyContainer RootControl { get; }

    private InteractionPropertyTree(
        StateMachine stateMachine,
        IInteractionState rootState,
        Entity document)
    {
        _stateMachine = stateMachine;
        RootControl = BuildBranch(rootState, document);
        RefreshVisibility();
    }

    public static InteractionPropertyTree Build(
        StateMachine stateMachine,
        IInteractionState rootState,
        Entity document)
    {
        return new(stateMachine, rootState, document);
    }

    // Stateless reports true for both the current leaf and each of its superstates. Therefore
    // parent scope properties stay visible while exactly the active child branch is shown.
    public void RefreshVisibility()
    {
        foreach (var (state, branch) in _stateBranches)
        {
            branch.Visible = _stateMachine.IsInState(state);
        }
    }

    private PropertyContainer BuildBranch(
        IInteractionState state,
        Entity document)
    {
        var branch = new PropertyContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        _stateBranches.Add(state, branch);

        var provider = state as IToolPropertyProvider;
        provider?.DrawPropertyBeforeSubstates(branch, document);

        // Always create the hierarchy slot. Hiding an empty leaf slot avoids layout spacing while
        // keeping the recursive algorithm and the two hook positions identical for every state.
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

        provider?.DrawPropertyAfterSubstates(branch, document);
        return branch;
    }
}
