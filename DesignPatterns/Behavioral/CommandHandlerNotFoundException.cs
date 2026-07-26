using System;

namespace DesignPatterns.Behavioral;

/// <summary>
/// Thrown when a command type has no registered handler on a command router.
/// </summary>
public sealed class CommandHandlerNotFoundException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CommandHandlerNotFoundException"/> class.
    /// </summary>
    public CommandHandlerNotFoundException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CommandHandlerNotFoundException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public CommandHandlerNotFoundException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CommandHandlerNotFoundException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public CommandHandlerNotFoundException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Creates an exception for a missing handler registration.
    /// </summary>
    /// <typeparam name="TCommand">The command type that has no handler.</typeparam>
    /// <returns>A descriptive exception instance.</returns>
    public static CommandHandlerNotFoundException ForCommand<TCommand>() =>
        new($"No command handler registered for command type '{typeof(TCommand)}'.");
}
