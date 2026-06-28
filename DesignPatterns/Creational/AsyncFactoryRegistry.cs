using System;
using System.Collections.Generic;
#if NET8_0_OR_GREATER
using System.Collections.Frozen;
#endif
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DesignPatterns.Creational;

/// <summary>
/// Immutable <see cref="IAsyncFactoryRegistry{TKey,TProduct}"/> backed by async factory delegates.
/// Each <see cref="CreateAsync"/> call creates a new instance.
/// </summary>
/// <typeparam name="TKey">Key type.</typeparam>
/// <typeparam name="TProduct">Product type.</typeparam>
public sealed class AsyncFactoryRegistry<TKey, TProduct> : IAsyncFactoryRegistry<TKey, TProduct>
    where TKey : notnull
{
    private readonly IReadOnlyDictionary<TKey, Func<CancellationToken, ValueTask<TProduct>>> _factories;

    /// <summary>
    /// Initializes a new instance from an existing async factory map.
    /// On net8.0+ the dictionary is frozen for faster lookups.
    /// </summary>
    public AsyncFactoryRegistry(
        IReadOnlyDictionary<TKey, Func<CancellationToken, ValueTask<TProduct>>> factories)
    {
        var dict = factories ?? throw new ArgumentNullException(nameof(factories));
#if NET8_0_OR_GREATER
        _factories = dict is FrozenDictionary<TKey, Func<CancellationToken, ValueTask<TProduct>>> frozen
            ? frozen
            : dict.ToFrozenDictionary();
#else
        _factories = dict;
#endif
    }

    /// <inheritdoc />
    public IReadOnlyCollection<TKey> Keys => _factories.Keys.ToArray();

    /// <inheritdoc />
    public bool TryGet(TKey key, [MaybeNullWhen(false)] out TProduct value)
    {
        // IReadOnlyRegistry.TryGet — for async registry, this creates synchronously
        // only for sync-compatible factories. We attempt a non-blocking path.
        if (_factories.TryGetValue(key, out var factory))
        {
            var vt = factory(default);
            if (vt.IsCompletedSuccessfully)
            {
                value = vt.Result;
                return true;
            }
        }

        value = default!;
        return false;
    }

    /// <inheritdoc />
    public async ValueTask<(bool Success, TProduct? Product)> TryCreateAsync(
        TKey key,
        CancellationToken cancellationToken = default)
    {
        if (_factories.TryGetValue(key, out var factory))
        {
            var product = await factory(cancellationToken).ConfigureAwait(false);
            return (true, product);
        }

        return (false, default);
    }

    /// <inheritdoc />
    public async ValueTask<TProduct> CreateAsync(
        TKey key,
        CancellationToken cancellationToken = default)
    {
        if (_factories.TryGetValue(key, out var factory))
        {
            return await factory(cancellationToken).ConfigureAwait(false);
        }

        throw FactoryNotFoundException.ForKey(key);
    }
}
