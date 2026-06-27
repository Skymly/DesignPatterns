using System;

namespace DesignPatterns.Behavioral;

/// <summary>
/// Observes handler exceptions during a traced pipeline invocation.
/// The observer is notified before the exception is re-thrown, allowing
/// side-effects such as logging or metrics recording.
/// </summary>
/// <typeparam name="TContext">The context type flowing through the pipeline.</typeparam>
public interface IHandlerExceptionObserver<TContext>
{
    /// <summary>
    /// Called when a handler (or its guard) throws an exception during a
    /// traced invocation.
    /// </summary>
    /// <param name="context">The pipeline context at the time of the failure.</param>
    /// <param name="handlerIndex">The zero-based index of the failing handler.</param>
    /// <param name="handlerName">The display name of the failing handler.</param>
    /// <param name="exception">The exception thrown by the handler.</param>
    void OnHandlerException(TContext context, int handlerIndex, string handlerName, Exception exception);
}
