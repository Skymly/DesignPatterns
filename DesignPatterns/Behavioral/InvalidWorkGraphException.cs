using System;

namespace DesignPatterns.Behavioral;

/// <summary>
/// Thrown when a work graph cannot be built because its step set is empty or
/// structurally invalid (duplicate id, self-dependency, unknown dependency, or cycle).
/// </summary>
public sealed class InvalidWorkGraphException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InvalidWorkGraphException"/> class.
    /// </summary>
    public InvalidWorkGraphException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="InvalidWorkGraphException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public InvalidWorkGraphException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="InvalidWorkGraphException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public InvalidWorkGraphException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
