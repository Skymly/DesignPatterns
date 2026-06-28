using System;

namespace DesignPatterns.Behavioral;

/// <summary>
/// Records the outcome of a traced strategy execution via
/// <see cref="StrategyRegistryExtensions.ExecuteTracedAsync{TKey,TInput,TOutput}"/>.
/// </summary>
/// <typeparam name="TOutput">The strategy output type.</typeparam>
public sealed class StrategyExecutionTrace<TOutput>
{
    internal StrategyExecutionTrace(
        string key,
        StrategyExecutionStepStatus status,
        TOutput? output,
        Exception? exception,
        long elapsedMilliseconds)
    {
        Key = key;
        Status = status;
        Output = output;
        Exception = exception;
        ElapsedMilliseconds = elapsedMilliseconds;
    }

    /// <summary>
    /// The strategy key that was requested.
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// The outcome of the resolution and execution.
    /// </summary>
    public StrategyExecutionStepStatus Status { get; }

    /// <summary>
    /// The strategy output when <see cref="Status"/> is
    /// <see cref="StrategyExecutionStepStatus.Executed"/>; otherwise <c>default</c>.
    /// </summary>
    public TOutput? Output { get; }

    /// <summary>
    /// The exception thrown by the strategy when <see cref="Status"/> is
    /// <see cref="StrategyExecutionStepStatus.Failed"/>; otherwise <see langword="null"/>.
    /// </summary>
    public Exception? Exception { get; }

    /// <summary>
    /// The elapsed time in milliseconds for the resolution and execution.
    /// </summary>
    public long ElapsedMilliseconds { get; }

    /// <summary>
    /// <see langword="true"/> when the strategy executed successfully
    /// (<see cref="Status"/> is <see cref="StrategyExecutionStepStatus.Executed"/>).
    /// </summary>
    public bool Succeeded => Status == StrategyExecutionStepStatus.Executed;
}
