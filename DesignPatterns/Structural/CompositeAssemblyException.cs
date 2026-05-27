using System;

namespace DesignPatterns.Structural;

/// <summary>
/// Thrown when a composite catalog cannot be assembled into a valid tree.
/// </summary>
public sealed class CompositeAssemblyException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CompositeAssemblyException"/> class.
    /// </summary>
    public CompositeAssemblyException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CompositeAssemblyException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public CompositeAssemblyException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CompositeAssemblyException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public CompositeAssemblyException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
