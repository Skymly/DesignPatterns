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
    {
        List<object> snapshot;

        lock (_lock)
        {
            if (!_handlers.TryGetValue(typeof(TEvent), out var list) || list.Count == 0)
            {
                return default;
            }

            snapshot = new List<object>(list);
        }

        return InvokeHandlersAsync(snapshot, evt, cancellationToken);
    }

    private static async ValueTask InvokeHandlersAsync<TEvent>(
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
}
