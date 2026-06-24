using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DesignPatterns.Behavioral;

/// <summary>
/// Result of an async transition attempt.
/// </summary>
/// <typeparam name="TState">State enum type.</typeparam>
public readonly struct TransitionResult<TState>
    where TState : struct, Enum
{
    internal TransitionResult(bool succeeded, TState nextState)
    {
        Succeeded = succeeded;
        NextState = nextState;
    }

    /// <summary>
    /// <see langword="true"/> when the transition succeeded.
    /// </summary>
    public bool Succeeded { get; }

    /// <summary>
    /// The target state when <see cref="Succeeded"/> is <see langword="true"/>; otherwise <c>default</c>.
    /// </summary>
    public TState NextState { get; }

    /// <summary>
    /// Deconstructs the result into a success flag and the next state.
    /// </summary>
    public void Deconstruct(out bool succeeded, out TState nextState)
    {
        succeeded = Succeeded;
        nextState = NextState;
    }
}

/// <summary>
/// Immutable transition table for enum state and trigger types.
/// </summary>
/// <typeparam name="TState">State enum type.</typeparam>
/// <typeparam name="TTrigger">Trigger enum type.</typeparam>
public interface ITransitionTable<TState, TTrigger>
    where TState : struct, Enum
    where TTrigger : struct, Enum
{
    /// <summary>
    /// Initial state declared when the table was built.
    /// </summary>
    TState InitialState { get; }

    /// <summary>
    /// Returns <see langword="false"/> when <paramref name="current"/> and <paramref name="trigger"/> do not match a declared transition.
    /// </summary>
    bool TryTransition(TState current, TTrigger trigger, out TState next);

    /// <summary>
    /// Async variant that invokes entry/exit actions (if any) before returning the target state.
    /// When the edge has no actions, this is equivalent to <see cref="TryTransition"/> with no await.
    /// If an async action throws <see cref="OperationCanceledException"/>, the exception propagates
    /// and the transition is considered incomplete — the caller must handle the partial state
    /// (actions before the exception may have already executed).
    /// </summary>
    /// <param name="current">Current state.</param>
    /// <param name="trigger">Trigger that fires the transition.</param>
    /// <param name="cancellationToken">Token to cancel async actions.</param>
    /// <returns>A <see cref="TransitionResult{TState}"/> indicating success and the target state.</returns>
    ValueTask<TransitionResult<TState>> TryTransitionAsync(
        TState current,
        TTrigger trigger,
        CancellationToken cancellationToken);

    /// <summary>
    /// Triggers that have at least one outgoing edge from <paramref name="current"/>.
    /// Order matches declaration order in the builder or generated source.
    /// </summary>
    IReadOnlyList<TTrigger> GetAllowedTriggers(TState current);

    /// <summary>
    /// Whether any edge leaves <paramref name="current"/>.
    /// </summary>
    bool CanTransitionFrom(TState current);
}
