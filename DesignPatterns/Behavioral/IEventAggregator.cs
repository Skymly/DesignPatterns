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
    /// When a handler throws, the exception propagates immediately and
    /// remaining handlers are not invoked (equivalent to
    /// <see cref="EventPublishErrorHandling.StopOnError"/>).
    /// </summary>
    /// <typeparam name="TEvent">The event type.</typeparam>
    /// <param name="evt">The event instance.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    ValueTask PublishAsync<TEvent>(TEvent evt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes an event with a configurable error handling strategy.
    /// </summary>
    /// <typeparam name="TEvent">The event type.</typeparam>
    /// <param name="evt">The event instance.</param>
    /// <param name="errorHandling">
    /// Controls how handler exceptions are handled. See
    /// <see cref="EventPublishErrorHandling"/> for available strategies.
    /// </param>
    /// <param name="cancellationToken">A cancellation token.</param>
    ValueTask PublishAsync<TEvent>(
        TEvent evt,
        EventPublishErrorHandling errorHandling,
        CancellationToken cancellationToken = default);
}
