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

    /// <summary>
    /// Marks this factory as asynchronous. When <see langword="true"/>, the factory must
    /// implement <see cref="IAsyncFactory{TProduct}"/> and the generator emits an
    /// <c>IAsyncFactoryRegistry</c> in addition to the sync registry.
    /// When <see langword="false"/> (default), async detection is automatic based on
    /// <see cref="IAsyncFactory{TProduct}"/> implementation.
    /// </summary>
    public bool IsAsync { get; set; }

    /// <summary>
    /// Enables pooling with the specified maximum pool size per key.
    /// <c>0</c> (default) disables pooling. A positive value causes the generator to emit
    /// an <c>IPooledFactoryRegistry</c>. Only effective when the factory is async
    /// (implements <see cref="IAsyncFactory{TProduct}"/> or <see cref="IsAsync"/> is <see langword="true"/>).
    /// </summary>
    public int PoolSize { get; set; }
}

#endif
