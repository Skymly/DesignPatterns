using System;

namespace DesignPatterns.Behavioral;

/// <summary>
/// Specifies the execution order of a handler within a generated pipeline for the given context type.
/// Lower values run first. Use the generic attribute when the target framework supports generic attributes (C# 11+).
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
public sealed class HandlerOrderAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HandlerOrderAttribute"/> class.
    /// </summary>
    /// <param name="order">The handler order. Lower values run first.</param>
    /// <param name="contextType">The context type (<c>IHandler&lt;TContext&gt;</c> type argument).</param>
    public HandlerOrderAttribute(int order, Type contextType)
    {
        Order = order;
        ContextType = contextType ?? throw new ArgumentNullException(nameof(contextType));
    }

    /// <summary>
    /// The handler order. Lower values run first.
    /// </summary>
    public int Order { get; }

    /// <summary>
    /// The context type flowing through the pipeline.
    /// </summary>
    public Type ContextType { get; }
}
