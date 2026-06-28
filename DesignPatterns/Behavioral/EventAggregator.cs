using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DesignPatterns.Behavioral;

/// <summary>
/// A lightweight, thread-safe publish/subscribe event aggregator.
/// Handlers are invoked sequentially in subscription order.
/// </summary>
public sealed class EventAggregator : IEventAggregator
{
    private readonly Dictionary<Type, List<object>> _handlers = new();
    private readonly object _lock = new();

    /// <inheritdoc />
    public void Subscribe<TEvent>(IEventHandler<TEvent> handler)
    {
        if (handler is null)
        {
            throw new ArgumentNullException(nameof(handler));
        }

        lock (_lock)
        {
            if (!_handlers.TryGetValue(typeof(TEvent), out var list))
            {
                list = new List<object>();
                _handlers[typeof(TEvent)] = list;
            }

            list.Add(handler);
        }
    }

    /// <inheritdoc />
    public void Unsubscribe<TEvent>(IEventHandler<TEvent> handler)
    {
        if (handler is null)
        {
            throw new ArgumentNullException(nameof(handler));
        }

        lock (_lock)
        {
            if (_handlers.TryGetValue(typeof(TEvent), out var list))
            {
                list.Remove(handler);

                if (list.Count == 0)
                {
                    _handlers.Remove(typeof(TEvent));
                }
            }
        }
    }

    /// <inheritdoc />
    public ValueTask PublishAsync<TEvent>(TEvent evt, CancellationToken cancellationToken = default)
        => PublishAsync(evt, EventPublishErrorHandling.StopOnError, cancellationToken);

    /// <inheritdoc />
    public ValueTask PublishAsync<TEvent>(
        TEvent evt,
        EventPublishErrorHandling errorHandling,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        List<object> snapshot;

        lock (_lock)
        {
            if (!_handlers.TryGetValue(typeof(TEvent), out var list) || list.Count == 0)
            {
                return default;
            }

            snapshot = new List<object>(list);
        }

        return errorHandling switch
        {
            EventPublishErrorHandling.StopOnError => InvokeStopOnErrorAsync(snapshot, evt, cancellationToken),
            EventPublishErrorHandling.ContinueOnError => InvokeContinueOnErrorAsync(snapshot, evt, cancellationToken),
            EventPublishErrorHandling.AggregateErrors => InvokeAggregateErrorsAsync(snapshot, evt, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(errorHandling), errorHandling,
                $"Unknown {nameof(EventPublishErrorHandling)} value."),
        };
    }

    /// <inheritdoc />
    public ValueTask<EventPublicationTrace> PublishTracedAsync<TEvent>(
        TEvent evt,
        CancellationToken cancellationToken = default)
        => PublishTracedAsync(evt, EventPublishErrorHandling.StopOnError, null, cancellationToken);

    /// <inheritdoc />
    public ValueTask<EventPublicationTrace> PublishTracedAsync<TEvent>(
        TEvent evt,
        EventPublishErrorHandling errorHandling,
        CancellationToken cancellationToken = default)
        => PublishTracedAsync(evt, errorHandling, null, cancellationToken);

    /// <inheritdoc />
    public async ValueTask<EventPublicationTrace> PublishTracedAsync<TEvent>(
        TEvent evt,
        EventPublishErrorHandling errorHandling,
        IEventPublicationObserver<TEvent>? observer,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        List<object> snapshot;

        lock (_lock)
        {
            if (!_handlers.TryGetValue(typeof(TEvent), out var list) || list.Count == 0)
            {
                return new EventPublicationTrace(Array.Empty<EventPublicationStep>());
            }

            snapshot = new List<object>(list);
        }

        var names = new string[snapshot.Count];
        var statuses = new EventPublicationStepStatus[snapshot.Count];
        var exceptions = new Exception?[snapshot.Count];

        for (var i = 0; i < snapshot.Count; i++)
        {
            names[i] = snapshot[i].GetType().Name;
            statuses[i] = EventPublicationStepStatus.NotReached;
        }

        List<Exception>? aggregateExceptions = null;

        for (var i = 0; i < snapshot.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var handler = (IEventHandler<TEvent>)snapshot[i];

            try
            {
                await handler.HandleAsync(evt, cancellationToken).ConfigureAwait(false);
                statuses[i] = EventPublicationStepStatus.Completed;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                statuses[i] = EventPublicationStepStatus.Failed;
                exceptions[i] = ex;
                observer?.OnHandlerException(evt, i, names[i], ex);

                switch (errorHandling)
                {
                    case EventPublishErrorHandling.StopOnError:
                        // Mark remaining as NotReached (already set) and re-throw.
                        throw;

                    case EventPublishErrorHandling.ContinueOnError:
                        // Record and continue — don't throw.
                        break;

                    case EventPublishErrorHandling.AggregateErrors:
                        (aggregateExceptions ??= new List<Exception>()).Add(ex);
                        break;

                    default:
                        throw new ArgumentOutOfRangeException(nameof(errorHandling), errorHandling,
                            $"Unknown {nameof(EventPublishErrorHandling)} value.");
                }
            }
        }

        if (aggregateExceptions is not null)
        {
            throw new AggregateException(aggregateExceptions);
        }

        return BuildTrace(names, statuses, exceptions);
    }

    private static EventPublicationTrace BuildTrace(
        string[] names,
        EventPublicationStepStatus[] statuses,
        Exception?[] exceptions)
    {
        var steps = new EventPublicationStep[names.Length];

        for (var i = 0; i < names.Length; i++)
        {
            steps[i] = new EventPublicationStep(i, names[i], statuses[i], exceptions[i]);
        }

        return new EventPublicationTrace(steps);
    }

    private static async ValueTask InvokeStopOnErrorAsync<TEvent>(
        List<object> snapshot,
        TEvent evt,
        CancellationToken cancellationToken)
    {
        foreach (var handlerObj in snapshot)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var handler = (IEventHandler<TEvent>)handlerObj;
            await handler.HandleAsync(evt, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async ValueTask InvokeContinueOnErrorAsync<TEvent>(
        List<object> snapshot,
        TEvent evt,
        CancellationToken cancellationToken)
    {
        foreach (var handlerObj in snapshot)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var handler = (IEventHandler<TEvent>)handlerObj;
            try
            {
                await handler.HandleAsync(evt, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                // Silently swallow — continue to next handler
            }
        }
    }

    private static async ValueTask InvokeAggregateErrorsAsync<TEvent>(
        List<object> snapshot,
        TEvent evt,
        CancellationToken cancellationToken)
    {
        List<Exception>? exceptions = null;

        foreach (var handlerObj in snapshot)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var handler = (IEventHandler<TEvent>)handlerObj;
            try
            {
                await handler.HandleAsync(evt, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                (exceptions ??= new List<Exception>()).Add(ex);
            }
        }

        if (exceptions is not null)
        {
            throw new AggregateException(exceptions);
        }
    }
}
