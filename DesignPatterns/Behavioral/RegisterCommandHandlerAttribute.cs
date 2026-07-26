using System;

namespace DesignPatterns.Behavioral;

/// <summary>
/// Marks a class as a command handler implementation for compile-time registration.
/// Use the generic attribute when the target framework supports generic attributes (C# 11+).
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
public sealed class RegisterCommandHandlerAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RegisterCommandHandlerAttribute"/> class.
    /// </summary>
    /// <param name="for">The command type handled by this handler.</param>
    public RegisterCommandHandlerAttribute(Type @for)
    {
        For = @for ?? throw new ArgumentNullException(nameof(@for));
    }

    /// <summary>
    /// The command type handled by this handler.
    /// </summary>
    public Type For { get; }
}
