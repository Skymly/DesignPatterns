using System;

namespace DesignPatterns.Structural;

/// <summary>
/// Specifies the execution order of a decorator within a generated stack for the given service contract.
/// Lower values are outermost. Use the generic attribute when the target framework supports generic attributes (C# 11+).
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class DecoratorAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DecoratorAttribute"/> class.
    /// </summary>
    /// <param name="order">The decorator order. Lower values are outermost.</param>
    /// <param name="serviceType">The service contract type.</param>
    public DecoratorAttribute(int order, Type serviceType)
    {
        Order = order;
        ServiceType = serviceType ?? throw new ArgumentNullException(nameof(serviceType));
    }

    /// <summary>
    /// The decorator order. Lower values are outermost.
    /// </summary>
    public int Order { get; }

    /// <summary>
    /// The service contract type.
    /// </summary>
    public Type ServiceType { get; }
}
