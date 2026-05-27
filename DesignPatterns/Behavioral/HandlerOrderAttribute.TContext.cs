#if NET7_0_OR_GREATER

using System;

namespace DesignPatterns.Behavioral;

/// <summary>
/// Specifies the execution order of a handler within a generated pipeline for <typeparamref name="TContext"/>.
/// Lower values run first.
/// </summary>
/// <typeparam name="TContext">The context type flowing through the pipeline.</typeparam>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class HandlerOrderAttribute<TContext> : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HandlerOrderAttribute{TContext}"/> class.
    /// </summary>
    /// <param name="order">The handler order. Lower values run first.</param>
    public HandlerOrderAttribute(int order) => Order = order;

    /// <summary>
    /// The handler order. Lower values run first.
    /// </summary>
    public int Order { get; }
}

#endif
