using System;

namespace DesignPatterns.Behavioral;

/// <summary>
/// A single handler entry in an <see cref="EventPublicationTrace"/>.
/// </summary>
public readonly struct EventPublicationStep
{
    public EventPublicationStep(int index, string handlerName, EventPublicationStepStatus status)
        : this(index, handlerName, status, null)
    {
    }

    public EventPublicationStep(int index, string handlerName, EventPublicationStepStatus status, Exception? exception)
    {
        Index = index;
        HandlerName = handlerName;
        Status = status;
        Exception = exception;
    }

    /// <summary>
    /// Zero-based position in subscription order.
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// Display name for the handler (typically the handler type name).
    /// </summary>
    public string HandlerName { get; }

    /// <summary>
    /// Whether the handler completed, failed, or was not reached.
    /// </summary>
    public EventPublicationStepStatus Status { get; }

    /// <summary>
    /// The exception thrown by the handler when <see cref="Status"/> is
    /// <see cref="EventPublicationStepStatus.Failed"/>; otherwise <see langword="null"/>.
    /// </summary>
    public Exception? Exception { get; }
}
