#if NET7_0_OR_GREATER

using System;

namespace DesignPatterns.Behavioral;

/// <summary>
/// Marks a class as a strategy implementation for compile-time registration.
/// </summary>
/// <typeparam name="TContract">The strategy contract type (interface or base class).</typeparam>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
public sealed class RegisterStrategyAttribute<TContract> : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RegisterStrategyAttribute{TContract}"/> class.
    /// </summary>
    /// <param name="key">The key used to resolve this strategy.</param>
    public RegisterStrategyAttribute(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Key cannot be null or whitespace.", nameof(key));
        }

        Key = key;
    }

    /// <summary>
    /// The key used to resolve this strategy.
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// Optional name of a static guard method on the implementation class.
    /// When set, the method must have the signature
    /// <c>static bool Method(TKey key)</c>.
    /// The strategy is only resolved when the guard returns <see langword="true"/>.
    /// </summary>
    public string? Guard { get; set; }
}

#endif
