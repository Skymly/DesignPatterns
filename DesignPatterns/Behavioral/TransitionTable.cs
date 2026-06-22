using System;
using System.Collections.Generic;
#if NET8_0_OR_GREATER
using System.Collections.Frozen;
#endif
using System.Linq;

namespace DesignPatterns.Behavioral;

/// <summary>
/// Immutable <see cref="ITransitionTable{TState,TTrigger}"/> backed by a read-only edge map.
/// On net8.0+ the edge map is frozen for faster lookups.
/// </summary>
/// <typeparam name="TState">State enum type.</typeparam>
/// <typeparam name="TTrigger">Trigger enum type.</typeparam>
public sealed class TransitionTable<TState, TTrigger> : ITransitionTable<TState, TTrigger>
    where TState : struct, Enum
    where TTrigger : struct, Enum
{
    private readonly IReadOnlyDictionary<(TState From, TTrigger Trigger), TransitionTableBuilder<TState, TTrigger>.TransitionEdge> _edges;
    private readonly IReadOnlyDictionary<TState, IReadOnlyList<TTrigger>> _triggersByState;

    /// <summary>
    /// Initializes a new instance from builder data.
    /// </summary>
    internal TransitionTable(
        TState initialState,
        IReadOnlyDictionary<(TState From, TTrigger Trigger), TransitionTableBuilder<TState, TTrigger>.TransitionEdge> edges,
        IReadOnlyDictionary<TState, IReadOnlyList<TTrigger>> triggersByState)
    {
        _edges = FreezeEdges(edges ?? throw new ArgumentNullException(nameof(edges)));
        _triggersByState = triggersByState ?? throw new ArgumentNullException(nameof(triggersByState));
        InitialState = initialState;
    }

    /// <inheritdoc />
    public TState InitialState { get; }

    /// <inheritdoc />
    public bool TryTransition(TState current, TTrigger trigger, out TState next)
    {
        if (_edges.TryGetValue((current, trigger), out var edge))
        {
            if (edge.Guard is null || edge.Guard(current, trigger))
            {
                next = edge.To;
                return true;
            }
        }

        next = default;
        return false;
    }

    /// <inheritdoc />
    public IReadOnlyList<TTrigger> GetAllowedTriggers(TState current) =>
        _triggersByState.TryGetValue(current, out var triggers)
            ? triggers
            : Array.Empty<TTrigger>();

    /// <inheritdoc />
    public bool CanTransitionFrom(TState current) =>
        _triggersByState.TryGetValue(current, out var triggers) && triggers.Count > 0;

    private static IReadOnlyDictionary<(TState From, TTrigger Trigger), TransitionTableBuilder<TState, TTrigger>.TransitionEdge> FreezeEdges(
        IReadOnlyDictionary<(TState From, TTrigger Trigger), TransitionTableBuilder<TState, TTrigger>.TransitionEdge> edges)
    {
#if NET8_0_OR_GREATER
        return edges is FrozenDictionary<(TState From, TTrigger Trigger), TransitionTableBuilder<TState, TTrigger>.TransitionEdge> frozen
            ? frozen
            : edges.ToFrozenDictionary();
#else
        return edges;
#endif
    }
}
