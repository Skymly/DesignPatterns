using System;
using System.Collections.Generic;

namespace DesignPatterns.Behavioral;

/// <summary>
/// Builds an immutable <see cref="ITransitionTable{TState,TTrigger}"/>.
/// </summary>
/// <typeparam name="TState">State enum type.</typeparam>
/// <typeparam name="TTrigger">Trigger enum type.</typeparam>
public sealed class TransitionTableBuilder<TState, TTrigger>
    where TState : struct, Enum
    where TTrigger : struct, Enum
{
    private readonly Dictionary<(TState From, TTrigger Trigger), TState> _edges = new();
    private readonly Dictionary<TState, List<TTrigger>> _triggersByState = new();
    private bool _hasInitial;
    private TState _initial;

    /// <summary>
    /// Sets the initial state for the transition table.
    /// </summary>
    public TransitionTableBuilder<TState, TTrigger> WithInitial(TState initial)
    {
        _initial = initial;
        _hasInitial = true;
        return this;
    }

    /// <summary>
    /// Registers a directed transition edge.
    /// </summary>
    public TransitionTableBuilder<TState, TTrigger> Add(TState from, TTrigger trigger, TState to)
    {
        var key = (from, trigger);
        if (_edges.ContainsKey(key))
        {
            throw new ArgumentException(
                $"A transition is already registered for state '{from}' and trigger '{trigger}'.",
                nameof(trigger));
        }

        _edges.Add(key, to);

        if (!_triggersByState.TryGetValue(from, out var triggers))
        {
            triggers = new List<TTrigger>();
            _triggersByState[from] = triggers;
        }

        triggers.Add(trigger);
        return this;
    }

    /// <summary>
    /// Builds the transition table.
    /// </summary>
    public ITransitionTable<TState, TTrigger> Build()
    {
        if (!_hasInitial)
        {
            throw new InvalidOperationException(
                "Call WithInitial before Build to set the initial state for the transition table.");
        }

        var triggersByState = new Dictionary<TState, IReadOnlyList<TTrigger>>();
        foreach (var pair in _triggersByState)
        {
            triggersByState[pair.Key] = pair.Value.ToArray();
        }

        return new TransitionTable<TState, TTrigger>(_initial, _edges, triggersByState);
    }
}
