using System;
using System.Collections.Generic;

namespace DesignPatterns.Behavioral;

/// <summary>
/// Records which handlers ran during a traced event publication via
/// <see cref="IEventAggregator.PublishTracedAsync{TEvent}"/>.
/// </summary>
public sealed class EventPublicationTrace
{
    internal EventPublicationTrace(IReadOnlyList<EventPublicationStep> steps)
    {
        Steps = steps ?? throw new ArgumentNullException(nameof(steps));
    }

    /// <summary>
    /// Handler steps in subscription order, including handlers that were not reached.
    /// </summary>
    public IReadOnlyList<EventPublicationStep> Steps { get; }

    /// <summary>
    /// The number of handlers that were subscribed for the event type.
    /// </summary>
    public int HandlerCount => Steps.Count;

    /// <summary>
    /// The zero-based index of the first handler that threw an exception,
    /// or <c>-1</c> when no handler failed.
    /// </summary>
    public int FailedHandlerIndex
    {
        get
        {
            for (var i = 0; i < Steps.Count; i++)
            {
                if (Steps[i].Status == EventPublicationStepStatus.Failed)
                {
                    return Steps[i].Index;
                }
            }

            return -1;
        }
    }

    /// <summary>
    /// The first exception thrown by a handler, or <see langword="null"/>
    /// when no handler failed. When multiple handlers failed, subsequent
    /// exceptions are available on individual <see cref="Steps"/>.
    /// </summary>
    public Exception? Exception
    {
        get
        {
            for (var i = 0; i < Steps.Count; i++)
            {
                if (Steps[i].Status == EventPublicationStepStatus.Failed)
                {
                    return Steps[i].Exception;
                }
            }

            return null;
        }
    }

    /// <summary>
    /// <see langword="true"/> when any handler failed.
    /// </summary>
    public bool HasFailures
    {
        get
        {
            for (var i = 0; i < Steps.Count; i++)
            {
                if (Steps[i].Status == EventPublicationStepStatus.Failed)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
