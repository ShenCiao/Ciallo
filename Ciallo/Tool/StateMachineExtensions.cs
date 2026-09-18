using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Stateless;
using Stateless.Reflection;

namespace Ciallo.Tool;

using StateMachine = StateMachine<InteractionState, Trigger>;

public static class StateMachineExtensions
{
    /// <summary>
    /// Derives input routes from a completed configuration. The caller owns the
    /// returned index; physical InputMap bindings and guards stay live at dispatch.
    /// </summary>
    public static Dictionary<TState, Trigger[]> BuildInputRoutes<TState>(
        this Stateless.StateMachine<TState, Trigger> machine) where TState : notnull =>
        machine.GetInfo().States.ToDictionary(
            state => (TState)state.UnderlyingState,
            state => CollectInputs(state).ToArray());

    public static bool TryHandleInput<TState>(
        this Stateless.StateMachine<TState, Trigger> machine,
        InputEvent input,
        IReadOnlyDictionary<TState, Trigger[]> routes) where TState : notnull
    {
        foreach (var trigger in routes[machine.State])
        {
            // Unrelated parameterized business triggers must not have their guards
            // evaluated with missing arguments. Query only matching input triggers.
            if (!trigger.Matches(input) || !machine.CanFire(trigger)) continue;

            if (input is not InputEventKey { Echo: true })
                machine.Fire(trigger);
            return true;
        }
        return false;
    }

    private static IEnumerable<Trigger> CollectInputs(StateInfo state)
    {
        var seen = new HashSet<Trigger>();
        for (var current = state; current != null; current = current.Superstate)
        {
            // InternalTransition appears in Transitions; Ignore has its own
            // collection. Neither enumeration is used as a priority rule.
            var inputs = current.Transitions.Concat(current.IgnoredTriggers)
                .Select(transition => (Trigger)transition.Trigger.UnderlyingTrigger)
                .Where(trigger => trigger.IsInput)
                .Distinct()
                .OrderByDescending(trigger => trigger.IsActionInput)
                .ThenBy(trigger => trigger.Name, StringComparer.Ordinal);

            foreach (var trigger in inputs)
                if (seen.Add(trigger))
                    yield return trigger;
        }
    }

    /// <summary>
    /// Permits Ciallo's standard exit triggers to transition to a target state, usually hover state.
    /// These are the common ways an interaction ends: user cancels, user confirms, or input capture is lost.
    /// </summary>
    public static StateMachine.StateConfiguration PermitStandardExits<TState>(
        this StateMachine.StateConfiguration config,
        TState fallback)
        where TState : InteractionState
    {
        return config
            .Permit(InteractionManager.CancelRequested, fallback)
            .Permit(InteractionManager.ConfirmRequested, fallback)
            .Permit(InteractionManager.InputCaptureLost, fallback);
    }

    /// <summary>
    /// Permits standard exit triggers to transition to a dynamically determined state.
    /// </summary>
    public static StateMachine.StateConfiguration PermitStandardExitsDynamic(
        this StateMachine.StateConfiguration config,
        Func<InteractionState> fallbackSelector)
    {
        return config
            .PermitDynamic(InteractionManager.CancelRequested, fallbackSelector)
            .PermitDynamic(InteractionManager.ConfirmRequested, fallbackSelector)
            .PermitDynamic(InteractionManager.InputCaptureLost, fallbackSelector);
    }
}
