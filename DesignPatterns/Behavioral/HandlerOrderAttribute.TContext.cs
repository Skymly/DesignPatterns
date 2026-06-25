#if NET7_0_OR_GREATER

using System;

namespace DesignPatterns.Behavioral;

/// <summary>
/// Specifies the execution order of a handler within a generated pipeline for <typeparamref name="TContext"/>.
/// Lower values run first.
/// </summary>
/// <typeparam name="TContext">The context type flowing through the pipeline.</typeparam>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
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

    /// <summary>
    /// Optional name of a static guard method on the handler class.
    /// When set, the method must have the signature
    /// <c>static bool Method(TContext context)</c>.
    /// The handler only executes when the guard returns <see langword="true"/>;
    /// otherwise the handler is skipped and the pipeline continues.
    /// </summary>
    public string? Guard { get; set; }
}

#endif
