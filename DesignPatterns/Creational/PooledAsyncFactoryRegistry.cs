using System;
using System.Collections.Concurrent;
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
/// Immutable <see cref="IPooledFactoryRegistry{TKey,TProduct}"/> with per-key object pools.
/// Uses <see cref="ConcurrentQueue{T}"/> per key for thread-safe rent/return.
/// When the pool is empty, a new instance is created via the registered async factory.
/// When the pool is full, returned products are silently discarded.
/// </summary>
/// <typeparam name="TKey">Key type.</typeparam>
/// <typeparam name="TProduct">Product type (must be a reference type).</typeparam>
public sealed class PooledAsyncFactoryRegistry<TKey, TProduct> : IPooledFactoryRegistry<TKey, TProduct>
    where TKey : notnull
    where TProduct : class
{
    private readonly IReadOnlyDictionary<TKey, Func<CancellationToken, ValueTask<TProduct>>> _factories;
    private readonly ConcurrentDictionary<TKey, ConcurrentQueue<TProduct>> _pools;
    private readonly int _maxPoolSize;

    /// <summary>
    /// Initializes a new instance from an existing async factory map with pooling.
    /// </summary>
    /// <param name="factories">Factory delegates keyed by product key.</param>
    /// <param name="poolSize">Maximum pool size per key (must be positive).</param>
    public PooledAsyncFactoryRegistry(
        IReadOnlyDictionary<TKey, Func<CancellationToken, ValueTask<TProduct>>> factories,
        int poolSize)
    {
        if (factories is null)
        {
            throw new ArgumentNullException(nameof(factories));
        }

        if (poolSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(poolSize), poolSize,
                "Pool size must be a positive integer.");
        }

#if NET8_0_OR_GREATER
        _factories = factories is FrozenDictionary<TKey, Func<CancellationToken, ValueTask<TProduct>>> frozen
            ? frozen
            : factories.ToFrozenDictionary();
#else
        _factories = factories;
#endif
        _maxPoolSize = poolSize;
        _pools = new ConcurrentDictionary<TKey, ConcurrentQueue<TProduct>>();

        foreach (var key in _factories.Keys)
        {
            _pools[key] = new ConcurrentQueue<TProduct>();
        }
    }

    /// <inheritdoc />
    public IReadOnlyCollection<TKey> Keys => _factories.Keys.ToArray();

    /// <inheritdoc />
    public bool TryGet(TKey key, [MaybeNullWhen(false)] out TProduct value)
    {
        // Try to get from pool synchronously first.
        if (_pools.TryGetValue(key, out var pool) && pool.TryDequeue(out var pooled))
        {
            value = pooled;
            return true;
        }

        value = default!;
        return false;
    }

    /// <inheritdoc />
    public async ValueTask<(bool Success, TProduct? Product)> TryCreateAsync(
        TKey key,
        CancellationToken cancellationToken = default)
    {
        if (!_factories.TryGetValue(key, out var factory))
        {
            return (false, default);
        }

        var product = await factory(cancellationToken).ConfigureAwait(false);
        return (true, product);
    }

    /// <inheritdoc />
    public async ValueTask<TProduct> CreateAsync(
        TKey key,
        CancellationToken cancellationToken = default)
    {
        if (!_factories.TryGetValue(key, out var factory))
        {
            throw FactoryNotFoundException.ForKey(key);
        }

        return await factory(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<TProduct> RentAsync(
        TKey key,
        CancellationToken cancellationToken = default)
    {
        // Try pool first (synchronous fast path).
        if (_pools.TryGetValue(key, out var pool) && pool.TryDequeue(out var pooled))
        {
            return pooled;
        }

        // Pool empty — create new instance.
        if (!_factories.TryGetValue(key, out var factory))
        {
            throw FactoryNotFoundException.ForKey(key);
        }

        return await factory(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Return(TKey key, TProduct product)
    {
        if (product is null)
        {
            throw new ArgumentNullException(nameof(product));
        }

        if (!_pools.TryGetValue(key, out var pool))
        {
            // Key not registered — discard product.
            return;
        }

        // Reset if the product implements IResettable.
        if (product is IResettable resettable)
        {
            resettable.Reset();
        }

        // Check pool capacity (benign race — worst case a few extra items).
        if (pool.Count >= _maxPoolSize)
        {
            // Pool full — discard product.
            return;
        }

        pool.Enqueue(product);
    }
}
