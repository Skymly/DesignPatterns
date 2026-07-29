#if NET7_0_OR_GREATER

using System;

namespace DesignPatterns.Behavioral;

/// <summary>
/// Marks a class as a command pipeline behavior for compile-time registration for <typeparamref name="TCommand"/>.
/// Lower <see cref="Order"/> values run first (outermost inbound).
/// </summary>
/// <typeparam name="TCommand">The command type this behavior applies to.</typeparam>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
public sealed class CommandPipelineBehaviorAttribute<TCommand> : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CommandPipelineBehaviorAttribute{TCommand}"/> class.
    /// </summary>
    /// <param name="order">Inbound order; lower values are outermost.</param>
    public CommandPipelineBehaviorAttribute(int order) => Order = order;

    /// <summary>
    /// Inbound order; lower values are outermost.
    /// </summary>
    public int Order { get; }
}

#endif
