using System;
using System.Threading;
using System.Threading.Tasks;

namespace DesignPatterns.Behavioral;

/// <summary>
/// Internal edge record storing the target state, optional guard, and optional
/// entry/exit action delegates. Shared by <see cref="TransitionTableBuilder{TState,TTrigger}"/>
/// and <see cref="TransitionTable{TState,TTrigger}"/>.
/// </summary>
/// <typeparam name="TState">State enum type.</typeparam>
/// <typeparam name="TTrigger">Trigger enum type.</typeparam>
internal readonly struct TransitionEdge<TState, TTrigger>
    where TState : struct, Enum
    where TTrigger : struct, Enum
{
    public TransitionEdge(
        TState to,
        Func<TState, TTrigger, bool>? guard,
        Action<TState, TState, TTrigger>? onEnterSync = null,
        Action<TState, TState, TTrigger>? onExitSync = null,
        Func<TState, TState, TTrigger, CancellationToken, ValueTask>? onEnterAsync = null,
        Func<TState, TState, TTrigger, CancellationToken, ValueTask>? onExitAsync = null)
    {
        To = to;
        Guard = guard;
        OnEnterSync = onEnterSync;
        OnExitSync = onExitSync;
        OnEnterAsync = onEnterAsync;
        OnExitAsync = onExitAsync;
    }

    public TState To { get; }

    public Func<TState, TTrigger, bool>? Guard { get; }

    public Action<TState, TState, TTrigger>? OnEnterSync { get; }

    public Action<TState, TState, TTrigger>? OnExitSync { get; }

    public Func<TState, TState, TTrigger, CancellationToken, ValueTask>? OnEnterAsync { get; }

    public Func<TState, TState, TTrigger, CancellationToken, ValueTask>? OnExitAsync { get; }

    /// <summary>
    /// <c>true</c> when the edge has any action (sync or async, enter or exit).
    /// </summary>
    public bool HasActions =>
        OnEnterSync is not null || OnExitSync is not null
        || OnEnterAsync is not null || OnExitAsync is not null;
}
