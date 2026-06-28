using System;
using System.Collections.Generic;

namespace DesignPatterns.Behavioral;

/// <summary>
/// Convenience extensions for querying hierarchy metadata on transition tables.
/// </summary>
public static class StateHierarchyExtensions
{
    /// <summary>
    /// Attempts to cast <paramref name="table"/> to <see cref="IStateHierarchy{TState}"/>
    /// and query <see cref="IStateHierarchy{TState}.IsInState"/>.
    /// Returns <see langword="false"/> when the table is not hierarchical.
    /// </summary>
    /// <typeparam name="TState">State enum type.</typeparam>
    /// <typeparam name="TTrigger">Trigger enum type.</typeparam>
    /// <param name="table">The transition table.</param>
    /// <param name="current">The state to check.</param>
    /// <param name="ancestor">The potential ancestor state.</param>
    /// <returns>
    /// <see langword="true"/> when the table is hierarchical and
    /// <paramref name="current"/> is <paramref name="ancestor"/> or a descendant;
    /// otherwise <see langword="false"/>.
    /// </returns>
    public static bool IsInState<TState, TTrigger>(
        this ITransitionTable<TState, TTrigger> table,
        TState current,
        TState ancestor)
        where TState : struct, Enum
        where TTrigger : struct, Enum
        => table is IStateHierarchy<TState> hierarchy && hierarchy.IsInState(current, ancestor);

    /// <summary>
    /// Attempts to cast <paramref name="table"/> to <see cref="IStateHierarchy{TState}"/>
    /// and query <see cref="IStateHierarchy{TState}.GetParent"/>.
    /// Returns <see langword="null"/> when the table is not hierarchical or
    /// when <paramref name="state"/> is a root.
    /// </summary>
    /// <typeparam name="TState">State enum type.</typeparam>
    /// <typeparam name="TTrigger">Trigger enum type.</typeparam>
    /// <param name="table">The transition table.</param>
    /// <param name="state">The state whose parent to retrieve.</param>
    /// <returns>The parent state, or <see langword="null"/> if not hierarchical or <paramref name="state"/> is a root.</returns>
    public static TState? GetParent<TState, TTrigger>(
        this ITransitionTable<TState, TTrigger> table,
        TState state)
        where TState : struct, Enum
        where TTrigger : struct, Enum
        => table is IStateHierarchy<TState> hierarchy ? hierarchy.GetParent(state) : null;

    /// <summary>
    /// Attempts to cast <paramref name="table"/> to <see cref="IStateHierarchy{TState}"/>
    /// and query <see cref="IStateHierarchy{TState}.GetAncestors"/>.
    /// Returns an empty list when the table is not hierarchical or
    /// when <paramref name="state"/> is a root.
    /// </summary>
    /// <typeparam name="TState">State enum type.</typeparam>
    /// <typeparam name="TTrigger">Trigger enum type.</typeparam>
    /// <param name="table">The transition table.</param>
    /// <param name="state">The state whose ancestors to retrieve.</param>
    /// <returns>An ordered list of ancestor states, nearest first; empty if not hierarchical or root.</returns>
    public static IReadOnlyList<TState> GetAncestors<TState, TTrigger>(
        this ITransitionTable<TState, TTrigger> table,
        TState state)
        where TState : struct, Enum
        where TTrigger : struct, Enum
        => table is IStateHierarchy<TState> hierarchy
            ? hierarchy.GetAncestors(state)
            : Array.Empty<TState>();
}
