using System.Threading;
using System.Threading.Tasks;

namespace DesignPatterns.Behavioral;

/// <summary>
/// Dispatches commands to a single registered handler per command CLR type (1:1).
/// </summary>
/// <remarks>
/// Missing handlers fail explicitly: throwing <c>Send*</c> APIs raise
/// <see cref="CommandHandlerNotFoundException"/>; <c>TrySend*</c> APIs return
/// failure without throwing for the missing-handler case.
/// </remarks>
public interface ICommandRouter
{
    /// <summary>
    /// Sends a void-style command to its registered handler.
    /// </summary>
    /// <typeparam name="TCommand">The command CLR type used as the routing key.</typeparam>
    /// <param name="command">The command instance.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <exception cref="CommandHandlerNotFoundException">
    /// Thrown when no handler is registered for <typeparamref name="TCommand"/>.
    /// </exception>
    ValueTask SendAsync<TCommand>(TCommand command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a command to its registered handler and returns the typed result.
    /// </summary>
    /// <typeparam name="TCommand">The command CLR type used as the routing key.</typeparam>
    /// <typeparam name="TResult">The result type expected from the handler.</typeparam>
    /// <param name="command">The command instance.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The handler result.</returns>
    /// <exception cref="CommandHandlerNotFoundException">
    /// Thrown when no handler is registered for <typeparamref name="TCommand"/>.
    /// </exception>
    ValueTask<TResult> SendAsync<TCommand, TResult>(
        TCommand command,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tries to send a void-style command. Returns <see langword="false"/> when no handler is registered.
    /// </summary>
    /// <typeparam name="TCommand">The command CLR type used as the routing key.</typeparam>
    /// <param name="command">The command instance.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>
    /// <see langword="true"/> when a handler was found and invoked; otherwise <see langword="false"/>.
    /// </returns>
    ValueTask<bool> TrySendAsync<TCommand>(TCommand command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tries to send a command and obtain a typed result. Returns
    /// <see cref="CommandSendAttempt{TResult}.Failed"/> when no handler is registered.
    /// </summary>
    /// <typeparam name="TCommand">The command CLR type used as the routing key.</typeparam>
    /// <typeparam name="TResult">The result type expected from the handler.</typeparam>
    /// <param name="command">The command instance.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A send attempt describing success or missing-handler failure.</returns>
    ValueTask<CommandSendAttempt<TResult>> TrySendAsync<TCommand, TResult>(
        TCommand command,
        CancellationToken cancellationToken = default);
}
