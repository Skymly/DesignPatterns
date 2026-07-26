using System;
using System.Collections.Generic;
#if NET8_0_OR_GREATER
using System.Collections.Frozen;
#endif
using System.Threading;
using System.Threading.Tasks;

namespace DesignPatterns.Behavioral;

/// <summary>
/// Immutable <see cref="ICommandRouter"/> backed by a read-only command-type → handler map.
/// On net8.0+ the dictionary is frozen for faster lookups.
/// </summary>
/// <remarks>
/// After construction the map is immutable; concurrent sends are safe without additional locking.
/// Prefer <see cref="CommandRouterBuilder"/> for the manual registration path.
/// </remarks>
public sealed class CommandRouter : ICommandRouter
{
    private readonly IReadOnlyDictionary<Type, object> _handlers;

    /// <summary>
    /// Initializes a new instance from an existing handler map keyed by command CLR type.
    /// </summary>
    /// <param name="handlers">The command-type → handler map.</param>
    public CommandRouter(IReadOnlyDictionary<Type, object> handlers)
    {
        var dict = handlers ?? throw new ArgumentNullException(nameof(handlers));
#if NET8_0_OR_GREATER
        _handlers = dict is FrozenDictionary<Type, object> frozen
            ? frozen
            : dict.ToFrozenDictionary();
#else
        _handlers = Snapshot(dict);
#endif
    }

    /// <inheritdoc />
    public ValueTask SendAsync<TCommand>(TCommand command, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_handlers.TryGetValue(typeof(TCommand), out var registered))
        {
            throw CommandHandlerNotFoundException.ForCommand<TCommand>();
        }

        if (registered is not ICommandHandler<TCommand> handler)
        {
            throw CreateHandlerContractMismatchException(typeof(TCommand), expected: $"ICommandHandler<{typeof(TCommand).Name}>");
        }

        return handler.HandleAsync(command, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<TResult> SendAsync<TCommand, TResult>(
        TCommand command,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_handlers.TryGetValue(typeof(TCommand), out var registered))
        {
            throw CommandHandlerNotFoundException.ForCommand<TCommand>();
        }

        if (registered is not ICommandHandler<TCommand, TResult> handler)
        {
            throw CreateHandlerContractMismatchException(
                typeof(TCommand),
                expected: $"ICommandHandler<{typeof(TCommand).Name}, {typeof(TResult).Name}>");
        }

        return handler.HandleAsync(command, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<bool> TrySendAsync<TCommand>(TCommand command, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_handlers.TryGetValue(typeof(TCommand), out var registered))
        {
            return new ValueTask<bool>(false);
        }

        if (registered is not ICommandHandler<TCommand> handler)
        {
            throw CreateHandlerContractMismatchException(typeof(TCommand), expected: $"ICommandHandler<{typeof(TCommand).Name}>");
        }

        return InvokeVoidAsync(handler, command, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<CommandSendAttempt<TResult>> TrySendAsync<TCommand, TResult>(
        TCommand command,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_handlers.TryGetValue(typeof(TCommand), out var registered))
        {
            return new ValueTask<CommandSendAttempt<TResult>>(CommandSendAttempt<TResult>.Failed);
        }

        if (registered is not ICommandHandler<TCommand, TResult> handler)
        {
            throw CreateHandlerContractMismatchException(
                typeof(TCommand),
                expected: $"ICommandHandler<{typeof(TCommand).Name}, {typeof(TResult).Name}>");
        }

        return InvokeResultAsync(handler, command, cancellationToken);
    }

    private static async ValueTask<bool> InvokeVoidAsync<TCommand>(
        ICommandHandler<TCommand> handler,
        TCommand command,
        CancellationToken cancellationToken)
    {
        await handler.HandleAsync(command, cancellationToken).ConfigureAwait(false);
        return true;
    }

    private static async ValueTask<CommandSendAttempt<TResult>> InvokeResultAsync<TCommand, TResult>(
        ICommandHandler<TCommand, TResult> handler,
        TCommand command,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(command, cancellationToken).ConfigureAwait(false);
        return CommandSendAttempt<TResult>.FromResult(result);
    }

#if !NET8_0_OR_GREATER
    private static Dictionary<Type, object> Snapshot(IReadOnlyDictionary<Type, object> handlers)
    {
        var copy = new Dictionary<Type, object>(handlers.Count);
        foreach (var pair in handlers)
        {
            copy.Add(pair.Key, pair.Value);
        }

        return copy;
    }
#endif

    private static InvalidOperationException CreateHandlerContractMismatchException(Type commandType, string expected) =>
        new(
            $"Command type '{commandType}' is registered, but not as {expected}. " +
            "Register a matching handler contract or call the matching SendAsync overload.");
}
