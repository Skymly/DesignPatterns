namespace DesignPatterns.Behavioral;

/// <summary>
/// Outcome of a strategy resolution and execution during a traced invocation.
/// </summary>
public enum StrategyExecutionStepStatus
{
    /// <summary>
    /// The strategy was resolved and executed successfully.
    /// </summary>
    Executed = 0,

    /// <summary>
    /// The strategy key was not found in the registry.
    /// </summary>
    KeyNotFound = 1,

    /// <summary>
    /// The strategy was found but its guard predicate rejected the request.
    /// </summary>
    GuardRejected = 2,

    /// <summary>
    /// The strategy was resolved but threw an exception during execution.
    /// The exception is captured in the trace.
    /// </summary>
    Failed = 3,
}
