using System.Threading;
using System.Threading.Tasks;

namespace DesignPatterns.Behavioral;

/// <summary>
/// A lightweight publish/subscribe mechanism for in-process event routing.
/// </summary>
public interface IEventAggregator
{
    /// <summary>
    /// Subscribes a handler to events of type <typeparamref name="TEvent"/>.
    /// </summary>
    /// <typeparam name="TEvent">The event type.</typeparam>
    /// <param name="handler">The handler to subscribe.</param>
    void Subscribe<TEvent>(IEventHandler<TEvent> handler);

    /// <summary>
    /// Unsubscribes a handler from events of type <typeparamref name="TEvent"/>.
    /// </summary>
    /// <typeparam name="TEvent">The event type.</typeparam>
    /// <param name="handler">The handler to unsubscribe.</param>
    void Unsubscribe<TEvent>(IEventHandler<TEvent> handler);

    /// <summary>
    /// Publishes an event, invoking all subscribed handlers sequentially.
    /// </summary>
    /// <typeparam name="TEvent">The event type.</typeparam>
    /// <param name="evt">The event instance.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    ValueTask PublishAsync<TEvent>(TEvent evt, CancellationToken cancellationToken = default);
}
