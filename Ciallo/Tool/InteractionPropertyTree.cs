using System.Collections.Generic;
using Ciallo.Misc;
using Ciallo.Widget;
using Frent;
using Godot;
using Stateless;

namespace Ciallo.Tool;

using StateMachine = StateMachine<InteractionState, Trigger>;

// Hierarchical property composition around direct substates.
// Providers are singletons: create controls here, never retain them.
public interface IPropertyProvider
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

        var provider = state as IPropertyProvider;
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
