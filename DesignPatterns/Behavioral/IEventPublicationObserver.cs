using System;

namespace DesignPatterns.Behavioral;

/// <summary>
/// Observes handler exceptions during a traced event publication.
/// The observer is notified when a handler throws, allowing side-effects
/// such as logging or metrics recording.
/// </summary>
/// <typeparam name="TEvent">The event type.</typeparam>
public interface IEventPublicationObserver<in TEvent>
{
    /// <summary>
    /// Called when a handler throws an exception during a traced publication.
    /// </summary>
    /// <param name="evt">The event instance being published.</param>
    /// <param name="handlerIndex">The zero-based index of the failing handler.</param>
    /// <param name="handlerName">The display name of the failing handler.</param>
    /// <param name="exception">The exception thrown by the handler.</param>
    void OnHandlerException(TEvent evt, int handlerIndex, string handlerName, Exception exception);
}
