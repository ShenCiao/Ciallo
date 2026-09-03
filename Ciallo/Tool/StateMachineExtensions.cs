using System;
using Stateless;

namespace Ciallo.Tool;

using StateMachine = StateMachine<InteractionState, Trigger>;

public static class StateMachineExtensions
{
    /// <summary>
    /// Permits standard exit triggers to transition to a target state.
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
