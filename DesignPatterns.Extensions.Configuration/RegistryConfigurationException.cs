using System;

namespace DesignPatterns.Extensions.Configuration;

/// <summary>
/// Thrown when a configuration-backed strategy key cannot be resolved from a registry.
/// </summary>
public sealed class RegistryConfigurationException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RegistryConfigurationException"/> class.
    /// </summary>
    public RegistryConfigurationException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RegistryConfigurationException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public RegistryConfigurationException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RegistryConfigurationException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public RegistryConfigurationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
