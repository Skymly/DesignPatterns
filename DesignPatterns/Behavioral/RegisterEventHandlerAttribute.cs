using System;

namespace DesignPatterns.Behavioral;

/// <summary>
/// Marks a class as an event handler implementation for compile-time registration.
/// Use the generic attribute when the target framework supports generic attributes (C# 11+).
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
public sealed class RegisterEventHandlerAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RegisterEventHandlerAttribute"/> class.
    /// </summary>
    /// <param name="for">The event type handled by this handler.</param>
    public RegisterEventHandlerAttribute(Type @for)
    {
        For = @for ?? throw new ArgumentNullException(nameof(@for));
    }

    /// <summary>
    /// The event type handled by this handler.
    /// </summary>
    public Type For { get; }
}
