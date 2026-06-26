using System;
using System.Collections.Generic;
#if NET8_0_OR_GREATER
using System.Collections.Frozen;
#endif
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

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
    private readonly IReadOnlyDictionary<(TState From, TTrigger Trigger), TransitionEdge<TState, TTrigger>> _edges;
    private readonly IReadOnlyDictionary<TState, IReadOnlyList<TTrigger>> _triggersByState;

    /// <summary>
    /// Initializes a new instance from builder data.
    /// </summary>
    internal TransitionTable(
        TState initialState,
        IReadOnlyDictionary<(TState From, TTrigger Trigger), TransitionEdge<TState, TTrigger>> edges,
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
    public async ValueTask<TransitionResult<TState>> TryTransitionAsync(
        TState current,
        TTrigger trigger,
        CancellationToken cancellationToken)
    {
        if (_edges.TryGetValue((current, trigger), out var edge))
        {
            if (edge.Guard is null || edge.Guard(current, trigger))
            {
                // Invoke OnExit (sync first, then async) before leaving the source state.
                if (edge.OnExitSync is not null)
                {
                    edge.OnExitSync(current, edge.To, trigger);
                }

                if (edge.OnExitAsync is not null)
                {
                    await edge.OnExitAsync(current, edge.To, trigger, cancellationToken).ConfigureAwait(false);
                }

                // Invoke OnEnter (sync first, then async) before entering the target state.
                if (edge.OnEnterSync is not null)
                {
                    edge.OnEnterSync(current, edge.To, trigger);
                }

                if (edge.OnEnterAsync is not null)
                {
                    await edge.OnEnterAsync(current, edge.To, trigger, cancellationToken).ConfigureAwait(false);
                }

                return new TransitionResult<TState>(true, edge.To);
            }
        }

        return default;
    }

    /// <inheritdoc />
    public async ValueTask<TransitionTrace<TState>> TryTransitionTracedAsync(
        TState current,
        TTrigger trigger,
        CancellationToken cancellationToken)
    {
        if (!_edges.TryGetValue((current, trigger), out var edge))
        {
            return new TransitionTrace<TState>(false, default, true, true, null);
        }

        // Guard evaluation
        try
        {
            if (edge.Guard is not null && !edge.Guard(current, trigger))
            {
                return new TransitionTrace<TState>(false, default, true, true, null);
            }
        }
        catch (Exception ex)
        {
            return new TransitionTrace<TState>(false, default, false, false, ex);
        }

        var onExitCompleted = true;
        var onEnterCompleted = true;

        try
        {
            // OnExit (sync first, then async)
            if (edge.OnExitSync is not null)
            {
                edge.OnExitSync(current, edge.To, trigger);
            }

            if (edge.OnExitAsync is not null)
            {
                await edge.OnExitAsync(current, edge.To, trigger, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            onExitCompleted = false;
            onEnterCompleted = false;
            return new TransitionTrace<TState>(false, edge.To, onExitCompleted, onEnterCompleted, ex);
        }

        try
        {
            // OnEnter (sync first, then async)
            if (edge.OnEnterSync is not null)
            {
                edge.OnEnterSync(current, edge.To, trigger);
            }

            if (edge.OnEnterAsync is not null)
            {
                await edge.OnEnterAsync(current, edge.To, trigger, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            onEnterCompleted = false;
            return new TransitionTrace<TState>(false, edge.To, onExitCompleted, onEnterCompleted, ex);
        }

        return new TransitionTrace<TState>(true, edge.To, onExitCompleted, onEnterCompleted, null);
    }

    /// <inheritdoc />
    public IReadOnlyList<TTrigger> GetAllowedTriggers(TState current) =>
        _triggersByState.TryGetValue(current, out var triggers)
            ? triggers
            : Array.Empty<TTrigger>();

    /// <inheritdoc />
    public bool CanTransitionFrom(TState current) =>
        _triggersByState.TryGetValue(current, out var triggers) && triggers.Count > 0;

    private static IReadOnlyDictionary<(TState From, TTrigger Trigger), TransitionEdge<TState, TTrigger>> FreezeEdges(
        IReadOnlyDictionary<(TState From, TTrigger Trigger), TransitionEdge<TState, TTrigger>> edges)
    {
#if NET8_0_OR_GREATER
        return edges is FrozenDictionary<(TState From, TTrigger Trigger), TransitionEdge<TState, TTrigger>> frozen
            ? frozen
            : edges.ToFrozenDictionary();
#else
        return edges;
#endif
    }
}
