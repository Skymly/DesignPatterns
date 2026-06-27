using System;

namespace DesignPatterns.Behavioral;

/// <summary>
/// A single handler entry in a <see cref="HandlerPipelineTrace"/>.
/// </summary>
public readonly struct HandlerPipelineStep
{
    public HandlerPipelineStep(int index, string name, HandlerPipelineStepStatus status)
        : this(index, name, status, null)
    {
    }

    public HandlerPipelineStep(int index, string name, HandlerPipelineStepStatus status, Exception? exception)
    {
        Index = index;
        Name = name;
        Status = status;
        Exception = exception;
    }

    /// <summary>
    /// Zero-based position in registration order.
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// Display name for the handler (typically the handler type name).
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Whether the handler completed, short-circuited, or was not reached.
    /// </summary>
    public HandlerPipelineStepStatus Status { get; }

    /// <summary>
    /// The exception thrown by the handler when <see cref="Status"/> is
    /// <see cref="HandlerPipelineStepStatus.Failed"/>; otherwise <see langword="null"/>.
    /// </summary>
    public Exception? Exception { get; }
}
