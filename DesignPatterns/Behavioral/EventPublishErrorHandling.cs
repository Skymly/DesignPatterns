namespace DesignPatterns.Behavioral;

/// <summary>
/// Specifies how <see cref="IEventAggregator.PublishAsync"/> handles
/// exceptions thrown by individual handlers.
/// </summary>
public enum EventPublishErrorHandling
{
    /// <summary>
    /// Stop on the first handler exception and propagate it immediately.
    /// Remaining handlers are not invoked. This is the default behavior
    /// and matches the original <see cref="IEventAggregator.PublishAsync{TEvent}"/>
    /// semantics.
    /// </summary>
    StopOnError,

    /// <summary>
    /// Invoke all handlers regardless of exceptions. Exceptions are silently
    /// swallowed — the caller does not know any errors occurred. Use this
    /// when event delivery is more important than error reporting.
    /// </summary>
    ContinueOnError,

    /// <summary>
    /// Invoke all handlers, collect all exceptions, and throw them as an
    /// <see cref="System.AggregateException"/> after all handlers have been
    /// invoked. Use this when all handlers must run but the caller also
    /// needs to know about failures.
    /// </summary>
    AggregateErrors,
}
