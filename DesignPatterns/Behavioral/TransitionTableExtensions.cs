using System;

namespace DesignPatterns.Behavioral;

/// <summary>
/// Convenience extensions for <see cref="ITransitionTable{TState,TTrigger}"/>.
/// </summary>
public static class TransitionTableExtensions
{
    /// <summary>
    /// Resolves the next state for <paramref name="current"/> and <paramref name="trigger"/>.
    /// </summary>
    /// <exception cref="ArgumentNullException">When <paramref name="table"/> is null.</exception>
    /// <exception cref="InvalidTransitionException">When the transition is not registered.</exception>
    public static TState Transition<TState, TTrigger>(
        this ITransitionTable<TState, TTrigger> table,
        TState current,
        TTrigger trigger)
        where TState : struct, Enum
        where TTrigger : struct, Enum
    {
        if (table is null)
        {
            throw new ArgumentNullException(nameof(table));
        }

        if (table.TryTransition(current, trigger, out var next))
        {
            return next;
        }

        throw InvalidTransitionException.ForTransition(current, trigger);
    }
}
