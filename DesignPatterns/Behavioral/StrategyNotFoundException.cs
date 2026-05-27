using System;

namespace DesignPatterns.Behavioral;

/// <summary>
/// Thrown when a strategy key cannot be resolved from a registry.
/// </summary>
public sealed class StrategyNotFoundException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StrategyNotFoundException"/> class.
    /// </summary>
    public StrategyNotFoundException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StrategyNotFoundException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public StrategyNotFoundException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StrategyNotFoundException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public StrategyNotFoundException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Creates an exception for a missing key.
    /// </summary>
    public static StrategyNotFoundException ForKey<TKey>(TKey key)
        where TKey : notnull =>
        new($"No strategy registered for key '{key}'.");
}
