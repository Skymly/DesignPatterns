using System;
using System.Collections.Generic;

namespace DesignPatterns.Behavioral;

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
    /// Triggers that have at least one outgoing edge from <paramref name="current"/>.
    /// Order matches declaration order in the builder or generated source.
    /// </summary>
    IReadOnlyList<TTrigger> GetAllowedTriggers(TState current);

    /// <summary>
    /// Whether any edge leaves <paramref name="current"/>.
    /// </summary>
    bool CanTransitionFrom(TState current);
}
