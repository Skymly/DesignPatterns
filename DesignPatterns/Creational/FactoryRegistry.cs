using System;
using System.Collections.Generic;

namespace DesignPatterns.Creational;

/// <summary>
/// Immutable <see cref="IFactoryRegistry{TKey,TProduct}"/> backed by factory delegates.
/// </summary>
/// <typeparam name="TKey">Key type.</typeparam>
/// <typeparam name="TProduct">Product type.</typeparam>
public sealed class FactoryRegistry<TKey, TProduct> : IFactoryRegistry<TKey, TProduct>
    where TKey : notnull
{
    private readonly IReadOnlyDictionary<TKey, Func<TProduct>> _factories;

    /// <summary>
    /// Initializes a new instance from an existing factory map.
    /// </summary>
    public FactoryRegistry(IReadOnlyDictionary<TKey, Func<TProduct>> factories)
    {
        _factories = factories ?? throw new ArgumentNullException(nameof(factories));
    }

    /// <inheritdoc />
    public IReadOnlyCollection<TKey> Keys => (IReadOnlyCollection<TKey>)_factories.Keys;

    /// <inheritdoc />
    public bool TryGet(TKey key, out TProduct value) => TryCreate(key, out value);

    /// <inheritdoc />
    public bool TryCreate(TKey key, out TProduct product)
    {
        if (_factories.TryGetValue(key, out var factory))
        {
            product = factory();
            return true;
        }

        product = default!;
        return false;
    }

    /// <inheritdoc />
    public TProduct Create(TKey key)
    {
        if (TryCreate(key, out var product))
        {
            return product;
        }

        throw FactoryNotFoundException.ForKey(key);
    }
}
