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
