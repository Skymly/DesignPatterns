using System;

namespace DesignPatterns.Behavioral;

/// <summary>
/// Marks a class as a strategy implementation for compile-time registration.
/// Use the generic attribute when the target framework supports generic attributes (C# 11+).
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
public sealed class RegisterStrategyAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RegisterStrategyAttribute"/> class.
    /// </summary>
    /// <param name="key">The key used to resolve this strategy.</param>
    /// <param name="for">The strategy contract type (interface or base class).</param>
    public RegisterStrategyAttribute(string key, Type @for)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Key cannot be null or whitespace.", nameof(key));
        }

        Key = key;
        For = @for ?? throw new ArgumentNullException(nameof(@for));
    }

    /// <summary>
    /// The key used to resolve this strategy.
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// The strategy contract type.
    /// </summary>
    public Type For { get; }
}
