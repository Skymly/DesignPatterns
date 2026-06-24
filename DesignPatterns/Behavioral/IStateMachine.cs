using System;
using System.Threading;
using System.Threading.Tasks;

namespace DesignPatterns.Behavioral;

/// <summary>
/// Stateful wrapper around an <see cref="ITransitionTable{TState,TTrigger}"/> that
/// automatically tracks <see cref="CurrentState"/> and fires entry/exit actions on
/// each transition.
/// </summary>
/// <typeparam name="TState">State enum type.</typeparam>
/// <typeparam name="TTrigger">Trigger enum type.</typeparam>
public interface IStateMachine<TState, TTrigger>
    where TState : struct, Enum
    where TTrigger : struct, Enum
{
    /// <summary>
    /// Current state of the machine. Set to <see cref="ITransitionTable{TState,TTrigger}.InitialState"/>
    /// on construction; updated automatically after each successful transition.
    /// </summary>
    TState CurrentState { get; set; }

    /// <summary>
    /// The underlying transition table.
    /// </summary>
    ITransitionTable<TState, TTrigger> Table { get; }

    /// <summary>
    /// Attempts a synchronous transition from <see cref="CurrentState"/> via <paramref name="trigger"/>.
    /// On success, <see cref="CurrentState"/> is updated to the target state.
    /// </summary>
    /// <param name="trigger">Trigger to fire.</param>
    /// <param name="nextState">The target state when successful; otherwise <c>default</c>.</param>
    /// <returns><see langword="true"/> when the transition succeeded.</returns>
    bool TryTransition(TTrigger trigger, out TState nextState);

    /// <summary>
    /// Async variant that invokes entry/exit actions (if any) before updating <see cref="CurrentState"/>.
    /// </summary>
    /// <param name="trigger">Trigger to fire.</param>
    /// <param name="cancellationToken">Token to cancel async actions.</param>
    /// <returns>A <see cref="TransitionResult{TState}"/> indicating success and the target state.</returns>
    ValueTask<TransitionResult<TState>> TryTransitionAsync(TTrigger trigger, CancellationToken cancellationToken);

    /// <summary>
    /// Fires <paramref name="trigger"/> and returns the new <see cref="CurrentState"/>.
    /// </summary>
    /// <exception cref="InvalidTransitionException">When the transition is not registered.</exception>
    TState Transition(TTrigger trigger);
}
