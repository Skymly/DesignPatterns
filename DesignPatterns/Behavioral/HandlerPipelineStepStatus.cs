namespace DesignPatterns.Behavioral;

/// <summary>
/// Outcome of a handler step during a traced pipeline invocation.
/// </summary>
public enum HandlerPipelineStepStatus
{
    /// <summary>
    /// The handler ran and invoked the next delegate.
    /// </summary>
    Completed = 0,

    /// <summary>
    /// The handler ran but did not invoke the next delegate, stopping the pipeline.
    /// </summary>
    ShortCircuited = 1,

    /// <summary>
    /// The handler was never invoked because an earlier handler short-circuited.
    /// </summary>
    NotReached = 2,
}
