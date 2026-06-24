using System;
using System.Threading;
using System.Threading.Tasks;

namespace DesignPatterns.Behavioral;

/// <summary>
/// Default implementation of <see cref="IStateMachine{TState,TTrigger}"/>.
/// Tracks <see cref="CurrentState"/> and delegates transitions to an
/// <see cref="ITransitionTable{TState,TTrigger}"/>.
/// </summary>
/// <typeparam name="TState">State enum type.</typeparam>
/// <typeparam name="TTrigger">Trigger enum type.</typeparam>
public class StateMachine<TState, TTrigger> : IStateMachine<TState, TTrigger>
    where TState : struct, Enum
    where TTrigger : struct, Enum
{
    private readonly ITransitionTable<TState, TTrigger> _table;

    /// <summary>
    /// Creates a new state machine backed by <paramref name="table"/>.
    /// <see cref="CurrentState"/> is initialized to <paramref name="table"/>'s
    /// <see cref="ITransitionTable{TState,TTrigger}.InitialState"/>.
    /// </summary>
    /// <param name="table">The transition table to use.</param>
    /// <exception cref="ArgumentNullException">When <paramref name="table"/> is null.</exception>
    public StateMachine(ITransitionTable<TState, TTrigger> table)
    {
        _table = table ?? throw new ArgumentNullException(nameof(table));
        CurrentState = _table.InitialState;
    }

    /// <inheritdoc />
    public TState CurrentState { get; set; }

    /// <inheritdoc />
    public ITransitionTable<TState, TTrigger> Table => _table;

    /// <inheritdoc />
    public bool TryTransition(TTrigger trigger, out TState nextState)
    {
        if (_table.TryTransition(CurrentState, trigger, out nextState))
        {
            CurrentState = nextState;
            return true;
        }

        return false;
    }

    /// <inheritdoc />
    public async ValueTask<TransitionResult<TState>> TryTransitionAsync(
        TTrigger trigger,
        CancellationToken cancellationToken)
    {
        var result = await _table.TryTransitionAsync(CurrentState, trigger, cancellationToken);
        if (result.Succeeded)
        {
            CurrentState = result.NextState;
        }

        return result;
    }

    /// <inheritdoc />
    public TState Transition(TTrigger trigger)
    {
        if (!TryTransition(trigger, out _))
        {
            throw InvalidTransitionException.ForTransition(CurrentState, trigger);
        }

        return CurrentState;
    }
}
