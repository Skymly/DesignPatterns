using System;

namespace DesignPatterns.Creational;

/// <summary>
/// Thrown when a factory key cannot be resolved from a registry.
/// </summary>
public sealed class FactoryNotFoundException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FactoryNotFoundException"/> class.
    /// </summary>
    public FactoryNotFoundException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FactoryNotFoundException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public FactoryNotFoundException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FactoryNotFoundException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public FactoryNotFoundException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Creates an exception for a missing key.
    /// </summary>
    public static FactoryNotFoundException ForKey<TKey>(TKey key)
        where TKey : notnull =>
        new($"No factory registered for key '{key}'.");
}
