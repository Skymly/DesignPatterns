using System;
using System.Collections.Generic;

namespace DesignPatterns.Behavioral;

/// <summary>
/// Builds an immutable <see cref="ICommandRouter"/> with a 1:1 command-type → handler map.
/// </summary>
/// <remarks>
/// The builder is not thread-safe. The router returned by <see cref="Build"/> is safe for
/// concurrent <see cref="ICommandRouter.SendAsync{TCommand}"/> / <c>TrySendAsync</c> calls.
/// </remarks>
public sealed class CommandRouterBuilder
{
    private readonly Dictionary<Type, object> _handlers = new();

    /// <summary>
    /// Registers a void-style handler for <typeparamref name="TCommand"/>.
    /// </summary>
    /// <typeparam name="TCommand">The command CLR type used as the routing key.</typeparam>
    /// <param name="handler">The handler instance.</param>
    /// <returns>This builder for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="handler"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A handler is already registered for <typeparamref name="TCommand"/>.</exception>
    public CommandRouterBuilder Register<TCommand>(ICommandHandler<TCommand> handler)
    {
        if (handler is null)
        {
            throw new ArgumentNullException(nameof(handler));
        }

        AddHandler(typeof(TCommand), handler);
        return this;
    }

    /// <summary>
    /// Registers a result-producing handler for <typeparamref name="TCommand"/>.
    /// </summary>
    /// <typeparam name="TCommand">The command CLR type used as the routing key.</typeparam>
    /// <typeparam name="TResult">The result type produced by the handler.</typeparam>
    /// <param name="handler">The handler instance.</param>
    /// <returns>This builder for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="handler"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A handler is already registered for <typeparamref name="TCommand"/>.</exception>
    public CommandRouterBuilder Register<TCommand, TResult>(ICommandHandler<TCommand, TResult> handler)
    {
        if (handler is null)
        {
            throw new ArgumentNullException(nameof(handler));
        }

        AddHandler(typeof(TCommand), handler);
        return this;
    }

    /// <summary>
    /// Builds an immutable command router.
    /// </summary>
    /// <returns>A thread-safe router for concurrent dispatch.</returns>
    public ICommandRouter Build() => new CommandRouter(new Dictionary<Type, object>(_handlers));

    private void AddHandler(Type commandType, object handler)
    {
        if (_handlers.ContainsKey(commandType))
        {
            throw new ArgumentException(
                $"A command handler is already registered for command type '{commandType}'.",
                nameof(handler));
        }

        _handlers.Add(commandType, handler);
    }
}
