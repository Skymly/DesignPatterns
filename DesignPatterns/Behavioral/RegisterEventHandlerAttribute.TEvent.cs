#if NET7_0_OR_GREATER

using System;

namespace DesignPatterns.Behavioral;

/// <summary>
/// Marks a class as an event handler implementation for compile-time registration.
/// This generic variant is available when the target framework supports generic attributes (C# 11+).
/// </summary>
/// <typeparam name="TEvent">The event type handled by this handler.</typeparam>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
public sealed class RegisterEventHandlerAttribute<TEvent> : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RegisterEventHandlerAttribute{TEvent}"/> class.
    /// </summary>
    public RegisterEventHandlerAttribute()
    {
    }
}

#endif
