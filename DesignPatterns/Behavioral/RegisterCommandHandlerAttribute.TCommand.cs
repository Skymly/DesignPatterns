#if NET7_0_OR_GREATER

using System;

namespace DesignPatterns.Behavioral;

/// <summary>
/// Marks a class as a command handler implementation for compile-time registration.
/// This generic variant is available when the target framework supports generic attributes (C# 11+).
/// </summary>
/// <typeparam name="TCommand">The command type handled by this handler.</typeparam>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
public sealed class RegisterCommandHandlerAttribute<TCommand> : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RegisterCommandHandlerAttribute{TCommand}"/> class.
    /// </summary>
    public RegisterCommandHandlerAttribute()
    {
    }
}

#endif
