using System;

namespace DesignPatterns.Behavioral;

/// <summary>
/// Internal edge record storing the target state and optional guard.
/// Shared by <see cref="TransitionTableBuilder{TState,TTrigger}"/> and
/// <see cref="TransitionTable{TState,TTrigger}"/>.
/// </summary>
/// <typeparam name="TState">State enum type.</typeparam>
/// <typeparam name="TTrigger">Trigger enum type.</typeparam>
internal readonly struct TransitionEdge<TState, TTrigger>
    where TState : struct, Enum
    where TTrigger : struct, Enum
{
    public TransitionEdge(TState to, Func<TState, TTrigger, bool>? guard)
    {
        To = to;
        Guard = guard;
    }

    public TState To { get; }

    public Func<TState, TTrigger, bool>? Guard { get; }
}
