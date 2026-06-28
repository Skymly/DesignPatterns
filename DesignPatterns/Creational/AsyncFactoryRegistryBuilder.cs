using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DesignPatterns.Creational;

/// <summary>
/// Builds an immutable <see cref="IAsyncFactoryRegistry{TKey,TProduct}"/> or
/// <see cref="IPooledFactoryRegistry{TKey,TProduct}"/>.
/// </summary>
/// <typeparam name="TKey">Key type.</typeparam>
/// <typeparam name="TProduct">Product type.</typeparam>
public sealed class AsyncFactoryRegistryBuilder<TKey, TProduct>
    where TKey : notnull
    where TProduct : class
{
    private readonly Dictionary<TKey, Func<CancellationToken, ValueTask<TProduct>>> _factories = new();
    private int? _poolSize;

    /// <summary>
    /// Registers an async factory delegate for the given key.
    /// </summary>
    /// <param name="key">The factory key.</param>
    /// <param name="factory">An async factory delegate that receives a cancellation token.</param>
    public AsyncFactoryRegistryBuilder<TKey, TProduct> Register(
        TKey key,
        Func<CancellationToken, ValueTask<TProduct>> factory)
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
    /// Registers a sync factory delegate (wrapped in async) for the given key.
    /// </summary>
    /// <param name="key">The factory key.</param>
    /// <param name="factory">A sync factory delegate.</param>
    public AsyncFactoryRegistryBuilder<TKey, TProduct> Register(
        TKey key,
        Func<TProduct> factory)
    {
        if (factory is null)
        {
            throw new ArgumentNullException(nameof(factory));
        }

        return Register(key, _ => new ValueTask<TProduct>(factory()));
    }

    /// <summary>
    /// Registers an <see cref="IAsyncFactory{TProduct}"/> for the given key.
    /// </summary>
    /// <param name="key">The factory key.</param>
    /// <param name="asyncFactory">An async factory instance.</param>
    public AsyncFactoryRegistryBuilder<TKey, TProduct> Register(
        TKey key,
        IAsyncFactory<TProduct> asyncFactory)
    {
        if (asyncFactory is null)
        {
            throw new ArgumentNullException(nameof(asyncFactory));
        }

        return Register(key, asyncFactory.CreateAsync);
    }

    /// <summary>
    /// Enables pooling with the specified maximum pool size per key.
    /// </summary>
    /// <param name="poolSize">Maximum pool size per key (must be positive).</param>
    public AsyncFactoryRegistryBuilder<TKey, TProduct> WithPooling(int poolSize = 16)
    {
        if (poolSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(poolSize), poolSize,
                "Pool size must be a positive integer.");
        }

        _poolSize = poolSize;
        return this;
    }

    /// <summary>
    /// Builds the registry. Returns a <see cref="IPooledFactoryRegistry{TKey,TProduct}"/>
    /// when pooling is enabled via <see cref="WithPooling"/>, otherwise an
    /// <see cref="IAsyncFactoryRegistry{TKey,TProduct}"/>.
    /// </summary>
    public IAsyncFactoryRegistry<TKey, TProduct> Build()
    {
        if (_poolSize.HasValue)
        {
            return new PooledAsyncFactoryRegistry<TKey, TProduct>(_factories, _poolSize.Value);
        }

        return new AsyncFactoryRegistry<TKey, TProduct>(_factories);
    }
}
