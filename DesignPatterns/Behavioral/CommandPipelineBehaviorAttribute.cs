using System;

namespace DesignPatterns.Behavioral;

/// <summary>
/// Marks a class as a command pipeline behavior for compile-time registration.
/// Lower <see cref="Order"/> values run first (outermost inbound).
/// Use the generic attribute when the target framework supports generic attributes (C# 11+).
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
public sealed class CommandPipelineBehaviorAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CommandPipelineBehaviorAttribute"/> class.
    /// </summary>
    /// <param name="order">Inbound order; lower values are outermost.</param>
    /// <param name="for">The command type this behavior applies to.</param>
    public CommandPipelineBehaviorAttribute(int order, Type @for)
    {
        Order = order;
        For = @for ?? throw new ArgumentNullException(nameof(@for));
    }

    /// <summary>
    /// Inbound order; lower values are outermost.
    /// </summary>
    public int Order { get; }

    /// <summary>
    /// The command type this behavior applies to.
    /// </summary>
    public Type For { get; }
}
