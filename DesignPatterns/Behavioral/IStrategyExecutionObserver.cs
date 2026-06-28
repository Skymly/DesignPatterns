using System;

namespace DesignPatterns.Behavioral;

/// <summary>
/// Observes strategy execution outcomes during a traced invocation.
/// Provides callbacks for successful and failed executions, allowing
/// side-effects such as logging, metrics, or audit trails.
/// </summary>
/// <typeparam name="TInput">The strategy input type.</typeparam>
/// <typeparam name="TOutput">The strategy output type.</typeparam>
public interface IStrategyExecutionObserver<in TInput, TOutput>
{
    /// <summary>
    /// Called when a strategy executes successfully.
    /// </summary>
    /// <param name="key">The strategy key.</param>
    /// <param name="input">The input passed to the strategy.</param>
    /// <param name="output">The strategy output.</param>
    /// <param name="elapsedMilliseconds">The elapsed time in milliseconds.</param>
    void OnExecutionCompleted(string key, TInput input, TOutput output, long elapsedMilliseconds);

    /// <summary>
    /// Called when a strategy execution fails or the key is not found.
    /// </summary>
    /// <param name="key">The strategy key.</param>
    /// <param name="input">The input passed to the strategy.</param>
    /// <param name="trace">The execution trace containing the failure details.</param>
    void OnExecutionFailed(string key, TInput input, StrategyExecutionTrace<TOutput> trace);
}
