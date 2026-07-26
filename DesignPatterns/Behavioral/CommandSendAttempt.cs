using System.Diagnostics.CodeAnalysis;

namespace DesignPatterns.Behavioral;

/// <summary>
/// Outcome of a <see cref="ICommandRouter.TrySendAsync{TCommand,TResult}"/> call.
/// </summary>
/// <typeparam name="TResult">The result type when a handler was found.</typeparam>
public readonly struct CommandSendAttempt<TResult>
{
    private CommandSendAttempt(bool success, TResult result)
    {
        Success = success;
        Result = result;
    }

    /// <summary>
    /// Gets a failed attempt (no handler registered). <see cref="Result"/> is <c>default</c>.
    /// </summary>
    public static CommandSendAttempt<TResult> Failed { get; } = new(success: false, result: default!);

    /// <summary>
    /// Creates a successful attempt with the given handler result.
    /// </summary>
    /// <param name="result">The handler result.</param>
    /// <returns>A successful send attempt.</returns>
    public static CommandSendAttempt<TResult> FromResult(TResult result) => new(success: true, result);

    /// <summary>
    /// Gets a value indicating whether a handler was found and invoked.
    /// </summary>
    public bool Success { get; }

    /// <summary>
    /// Gets the handler result when <see cref="Success"/> is <see langword="true"/>;
    /// otherwise <c>default</c>.
    /// </summary>
    [MaybeNull]
    public TResult Result { get; }
}
