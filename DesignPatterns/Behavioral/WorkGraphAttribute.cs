using System;

namespace DesignPatterns.Behavioral;

/// <summary>
/// Marks a holder type that names a work graph and fixes its shared context type.
/// Prefer a <c>static class</c> holder. Use the generic attribute when the target
/// framework supports generic attributes (C# 11+).
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class WorkGraphAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WorkGraphAttribute"/> class.
    /// </summary>
    /// <param name="contextType">The shared context type for steps in this graph.</param>
    public WorkGraphAttribute(Type contextType)
    {
        ContextType = contextType ?? throw new ArgumentNullException(nameof(contextType));
    }

    /// <summary>
    /// The shared context type for steps in this graph.
    /// </summary>
    public Type ContextType { get; }
}
