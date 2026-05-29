#if NET7_0_OR_GREATER

using System;

namespace DesignPatterns.Creational;

/// <summary>
/// Marks a class as a factory implementation for compile-time registration.
/// </summary>
/// <typeparam name="TContract">The factory contract type (interface or base class).</typeparam>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
public sealed class RegisterFactoryAttribute<TContract> : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RegisterFactoryAttribute{TContract}"/> class.
    /// </summary>
    /// <param name="key">The key used to resolve this factory.</param>
    public RegisterFactoryAttribute(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Key cannot be null or whitespace.", nameof(key));
        }

        Key = key;
    }

    /// <summary>
    /// The key used to resolve this factory.
    /// </summary>
    public string Key { get; }
}

#endif
