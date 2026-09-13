using Stateless;
using System.Collections.Immutable;
using System;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Ciallo.Misc;

public static class StatelessExtensions
{
    private static class SubstateMapCache<TState, TTrigger>
        where TState : notnull
    {
        public static readonly ConditionalWeakTable<
            StateMachine<TState, TTrigger>,
            ImmutableDictionary<TState, ImmutableArray<TState>>> Maps = new();
    }

    /// <summary>
    /// Snapshots the machine's direct-substate relationships.
    /// </summary>
    /// <remarks>
    /// Call once after every state and <c>SubstateOf</c> relationship has been configured.
    /// Configuring the machine hierarchy after this call does not update the snapshot.
    /// Direct substates retain Stateless's <c>SubstateOf</c> registration order.
    /// </remarks>
    public static void BuildSubstateMap<TState, TTrigger>(
        this StateMachine<TState, TTrigger> stateMachine)
        where TState : notnull
    {
        var map = stateMachine.GetInfo().States.ToImmutableDictionary(
            state => (TState)state.UnderlyingState,
            state => state.Substates
                .Select(substate => (TState)substate.UnderlyingState)
                .ToImmutableArray());

        SubstateMapCache<TState, TTrigger>.Maps.Add(stateMachine, map);
    }

    /// <summary>
    /// Returns the direct substates captured by <see cref="BuildSubstateMap{TState,TTrigger}"/>.
    /// </summary>
    public static ImmutableArray<TState> GetSubstates<TState, TTrigger>(
        this StateMachine<TState, TTrigger> stateMachine,
        TState state)
        where TState : notnull
    {
        if (!SubstateMapCache<TState, TTrigger>.Maps.TryGetValue(stateMachine, out var map))
        {
            throw new InvalidOperationException(
                "BuildSubstateMap must be called after the state-machine hierarchy is configured.");
        }

        return map[state];
    }
}
