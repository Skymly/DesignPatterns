namespace DesignPatterns.Behavioral;

/// <summary>
/// Outcome of a single handler invocation during a traced event publication.
/// </summary>
public enum EventPublicationStepStatus
{
    /// <summary>
    /// The handler ran and completed without throwing.
    /// </summary>
    Completed = 0,

    /// <summary>
    /// The handler threw an exception. The exception is captured in the trace.
    /// </summary>
    Failed = 1,

    /// <summary>
    /// The handler was never invoked because an earlier handler failed and
    /// the error handling strategy was <see cref="EventPublishErrorHandling.StopOnError"/>.
    /// </summary>
    NotReached = 2,
}
