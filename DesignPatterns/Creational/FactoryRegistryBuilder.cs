using System;
using System.Collections.Generic;

namespace DesignPatterns.Creational;

/// <summary>
/// Builds an immutable <see cref="IFactoryRegistry{TKey,TProduct}"/>.
/// </summary>
/// <typeparam name="TKey">Key type.</typeparam>
/// <typeparam name="TProduct">Product type.</typeparam>
public sealed class FactoryRegistryBuilder<TKey, TProduct>
    where TKey : notnull
{
    private readonly Dictionary<TKey, Func<TProduct>> _factories = new();

    /// <summary>
    /// Registers a factory delegate for the given key.
    /// </summary>
    public FactoryRegistryBuilder<TKey, TProduct> Register(TKey key, Func<TProduct> factory)
    {
        if (key is null)
        {
            throw new ArgumentNullException(nameof(key));
        }

        if (factory is null)
        {
            throw new ArgumentNullException(nameof(factory));
        }

        if (_factories.ContainsKey(key))
        {
            throw new ArgumentException($"A factory is already registered for key '{key}'.", nameof(key));
        }

        _factories.Add(key, factory);
        return this;
    }

    /// <summary>
    /// Registers a factory that receives the key when creating a product.
    /// </summary>
    public FactoryRegistryBuilder<TKey, TProduct> Register(TKey key, Func<TKey, TProduct> factory)
    {
        if (factory is null)
        {
            throw new ArgumentNullException(nameof(factory));
        }

        return Register(key, () => factory(key));
    }

    /// <summary>
    /// Builds the registry.
    /// </summary>
    public IFactoryRegistry<TKey, TProduct> Build() =>
        new FactoryRegistry<TKey, TProduct>(_factories);
}
