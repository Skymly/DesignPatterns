#if NET7_0_OR_GREATER

using System;

namespace DesignPatterns.Structural;

/// <summary>
/// Specifies the execution order of a decorator within a generated stack for <typeparamref name="TService"/>.
/// Lower values are outermost.
/// </summary>
/// <typeparam name="TService">The service contract type.</typeparam>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class DecoratorAttribute<TService> : Attribute
    where TService : class
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DecoratorAttribute{TService}"/> class.
    /// </summary>
    /// <param name="order">The decorator order. Lower values are outermost.</param>
    public DecoratorAttribute(int order) => Order = order;

    /// <summary>
    /// The decorator order. Lower values are outermost.
    /// </summary>
    public int Order { get; }
}

#endif
