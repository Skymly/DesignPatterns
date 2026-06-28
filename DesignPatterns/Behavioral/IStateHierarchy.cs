using System;
using System.Collections.Generic;

namespace DesignPatterns.Behavioral;

/// <summary>
/// Optional hierarchy metadata for hierarchical state machines.
/// <see cref="TransitionTable{TState,TTrigger}"/> always implements this interface;
/// when no parent relationships are declared, the methods return trivial results
/// (<see cref="GetParent"/> returns <see langword="null"/>,
/// <see cref="IsInState"/> returns <see langword="true"/> only for self,
/// <see cref="GetAncestors"/> returns an empty list).
/// </summary>
/// <typeparam name="TState">State enum type.</typeparam>
public interface IStateHierarchy<TState>
    where TState : struct, Enum
{
    /// <summary>
    /// Returns the parent state of <paramref name="state"/>, or <see langword="null"/>
    /// when <paramref name="state"/> is a root state (has no parent).
    /// </summary>
    /// <param name="state">The state whose parent to retrieve.</param>
    /// <returns>The parent state, or <see langword="null"/> if <paramref name="state"/> is a root.</returns>
    TState? GetParent(TState state);

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="current"/> is
    /// <paramref name="ancestor"/> itself or a descendant of <paramref name="ancestor"/>.
    /// </summary>
    /// <param name="current">The state to check.</param>
    /// <param name="ancestor">The potential ancestor state.</param>
    /// <returns>
    /// <see langword="true"/> when <paramref name="current"/> is <paramref name="ancestor"/>
    /// or a descendant; otherwise <see langword="false"/>.
    /// </returns>
    bool IsInState(TState current, TState ancestor);

    /// <summary>
    /// Returns the ancestor chain from <paramref name="state"/> up to the root
    /// (exclusive of the root itself), ordered from immediate parent to most distant ancestor.
    /// Returns an empty list when <paramref name="state"/> is a root.
    /// </summary>
    /// <param name="state">The state whose ancestors to retrieve.</param>
    /// <returns>An ordered list of ancestor states, nearest first.</returns>
    IReadOnlyList<TState> GetAncestors(TState state);
}
