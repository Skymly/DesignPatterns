using System;

namespace DesignPatterns.Creational;

/// <summary>
/// Marks a class as a factory implementation for compile-time registration.
/// Use the generic attribute when the target framework supports generic attributes (C# 11+).
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
public sealed class RegisterFactoryAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RegisterFactoryAttribute"/> class.
    /// </summary>
    /// <param name="key">The key used to resolve this factory.</param>
    /// <param name="contract">The factory contract type (interface or base class).</param>
    public RegisterFactoryAttribute(string key, Type contract)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Key cannot be null or whitespace.", nameof(key));
        }

        Key = key;
        Contract = contract ?? throw new ArgumentNullException(nameof(contract));
    }

    /// <summary>
    /// The key used to resolve this factory.
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// The factory contract type.
    /// </summary>
    public Type Contract { get; }
}
