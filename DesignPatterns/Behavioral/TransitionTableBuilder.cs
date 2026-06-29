using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

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
    private readonly Dictionary<(TState From, TTrigger Trigger), TransitionEdge<TState, TTrigger>> _edges = new();
    private readonly Dictionary<TState, List<TTrigger>> _triggersByState = new();
    private readonly Dictionary<TState, TState> _parents = new();
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
    /// Registers a directed transition edge without a guard.
    /// </summary>
    public TransitionTableBuilder<TState, TTrigger> Add(TState from, TTrigger trigger, TState to)
        => Add(from, trigger, to, guard: null);

    /// <summary>
    /// Registers a directed transition edge with optional named parameters for
    /// guard and sync/async actions. This overload uses default values so the
    /// caller can pass any subset of <c>guard</c>, <c>onEnterSync</c>,
    /// <c>onExitSync</c>, <c>onEnterAsync</c>, <c>onExitAsync</c> as named
    /// arguments without providing positional placeholders.
    /// </summary>
    /// <param name="from">Source state.</param>
    /// <param name="trigger">Trigger that fires the transition.</param>
    /// <param name="to">Target state.</param>
    /// <param name="guard">Optional guard delegate. When <see langword="null"/>, the transition always fires.</param>
    /// <param name="onEnterSync">Optional sync action invoked when entering the target state.</param>
    /// <param name="onExitSync">Optional sync action invoked when exiting the source state.</param>
    /// <param name="onEnterAsync">Optional async action invoked when entering the target state.</param>
    /// <param name="onExitAsync">Optional async action invoked when exiting the source state.</param>
    public TransitionTableBuilder<TState, TTrigger> Add(
        TState from,
        TTrigger trigger,
        TState to,
        Func<TState, TTrigger, bool>? guard = null,
        Action<TState, TState, TTrigger>? onEnterSync = null,
        Action<TState, TState, TTrigger>? onExitSync = null,
        Func<TState, TState, TTrigger, CancellationToken, ValueTask>? onEnterAsync = null,
        Func<TState, TState, TTrigger, CancellationToken, ValueTask>? onExitAsync = null)
    {
        var key = (from, trigger);
        if (_edges.ContainsKey(key))
        {
            throw new ArgumentException(
                $"A transition is already registered for state '{from}' and trigger '{trigger}'.",
                nameof(trigger));
        }

        _edges.Add(key, new TransitionEdge<TState, TTrigger>(
            to, guard, onEnterSync, onExitSync, onEnterAsync, onExitAsync));

        if (!_triggersByState.TryGetValue(from, out var triggers))
        {
            triggers = new List<TTrigger>();
            _triggersByState[from] = triggers;
        }

        triggers.Add(trigger);
        return this;
    }

    /// <summary>
    /// Declares a parent-child relationship for hierarchical state machine mode.
    /// After at least one <see cref="WithParent"/> call, the built table also
    /// implements <see cref="IStateHierarchy{TState}"/>.
    /// <para>
    /// The manual builder does <b>not</b> flatten inherited transitions —
    /// callers must add all effective edges explicitly. The hierarchy metadata
    /// is provided for <see cref="IStateHierarchy{TState}.IsInState"/> /
    /// <see cref="IStateHierarchy{TState}.GetParent"/> queries only.
    /// </para>
    /// </summary>
    /// <param name="child">The child state.</param>
    /// <param name="parent">The parent state. Must differ from <paramref name="child"/>.</param>
    /// <exception cref="ArgumentException">
    /// When <paramref name="parent"/> equals <paramref name="child"/> (self-reference)
    /// or when <paramref name="child"/> already has a different parent declared.
    /// </exception>
    public TransitionTableBuilder<TState, TTrigger> WithParent(TState child, TState parent)
    {
        if (child.Equals(parent))
        {
            throw new ArgumentException(
                $"A state cannot be its own parent: '{child}' was declared as child and parent.",
                nameof(parent));
        }

        if (_parents.TryGetValue(child, out var existingParent) && !existingParent.Equals(parent))
        {
            throw new ArgumentException(
                $"State '{child}' already has parent '{existingParent}'; multiple inheritance is not supported.",
                nameof(parent));
        }

        _parents[child] = parent;
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

        return new TransitionTable<TState, TTrigger>(
            _initial,
            _edges,
            triggersByState,
            _parents.Count > 0 ? _parents : null);
    }
}
