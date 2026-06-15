using System;

namespace DesignPatterns.Behavioral;

/// <summary>
/// Thrown when a state and trigger pair cannot be resolved from a transition table.
/// </summary>
public sealed class InvalidTransitionException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InvalidTransitionException"/> class.
    /// </summary>
    public InvalidTransitionException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="InvalidTransitionException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public InvalidTransitionException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="InvalidTransitionException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public InvalidTransitionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Creates an exception for a missing transition.
    /// </summary>
    public static InvalidTransitionException ForTransition<TState, TTrigger>(TState current, TTrigger trigger)
        where TState : struct, Enum
        where TTrigger : struct, Enum =>
        new($"No transition registered for state '{current}' and trigger '{trigger}'.");
}
