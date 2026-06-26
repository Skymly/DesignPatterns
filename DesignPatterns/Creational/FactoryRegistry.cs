using System;
using System.Collections.Generic;
#if NET8_0_OR_GREATER
using System.Collections.Frozen;
#endif
using System.Diagnostics.CodeAnalysis;
using System.Linq;

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
    /// On net8.0+ the dictionary is frozen for faster lookups.
    /// </summary>
    public FactoryRegistry(IReadOnlyDictionary<TKey, Func<TProduct>> factories)
    {
        var dict = factories ?? throw new ArgumentNullException(nameof(factories));
#if NET8_0_OR_GREATER
        _factories = dict is FrozenDictionary<TKey, Func<TProduct>> frozen
            ? frozen
            : dict.ToFrozenDictionary();
#else
        _factories = dict;
#endif
    }

    /// <inheritdoc />
    public IReadOnlyCollection<TKey> Keys => _factories.Keys.ToArray();

    /// <inheritdoc />
    public bool TryGet(TKey key, [MaybeNullWhen(false)] out TProduct value) => TryCreate(key, out value);

    /// <inheritdoc />
    public bool TryCreate(TKey key, [MaybeNullWhen(false)] out TProduct product)
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
