using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

namespace DesignPatterns.Creational;

/// <summary>
/// Extension methods for adapting sync factory registries to async.
/// </summary>
public static class FactoryRegistryAsyncExtensions
{
    /// <summary>
    /// Adapts a sync <see cref="IFactoryRegistry{TKey,TProduct}"/> to
    /// <see cref="IAsyncFactoryRegistry{TKey,TProduct}"/> by wrapping synchronous
    /// creation in <see cref="ValueTask{TResult}"/>.
    /// The <paramref name="cancellationToken"/> is ignored for sync factories.
    /// </summary>
    /// <typeparam name="TKey">Key type.</typeparam>
    /// <typeparam name="TProduct">Product type.</typeparam>
    /// <param name="registry">The sync factory registry to adapt.</param>
    /// <returns>An async wrapper around the sync registry.</returns>
    /// <exception cref="ArgumentNullException">When <paramref name="registry"/> is null.</exception>
    public static IAsyncFactoryRegistry<TKey, TProduct> AsAsync<TKey, TProduct>(
        this IFactoryRegistry<TKey, TProduct> registry)
        where TKey : notnull
    {
        if (registry is null)
        {
            throw new ArgumentNullException(nameof(registry));
        }

        return new AsyncFactoryRegistryAdapter<TKey, TProduct>(registry);
    }

    private sealed class AsyncFactoryRegistryAdapter<TKey, TProduct> : IAsyncFactoryRegistry<TKey, TProduct>
        where TKey : notnull
    {
        private readonly IFactoryRegistry<TKey, TProduct> _sync;

        public AsyncFactoryRegistryAdapter(IFactoryRegistry<TKey, TProduct> sync)
        {
            _sync = sync;
        }

        /// <inheritdoc />
        public System.Collections.Generic.IReadOnlyCollection<TKey> Keys => _sync.Keys;

        /// <inheritdoc />
        public bool TryGet(TKey key, [MaybeNullWhen(false)] out TProduct value) =>
            _sync.TryGet(key, out value);

        /// <inheritdoc />
        public ValueTask<(bool Success, TProduct? Product)> TryCreateAsync(
            TKey key,
            CancellationToken cancellationToken = default)
        {
            if (_sync.TryCreate(key, out var product))
            {
                return new ValueTask<(bool, TProduct?)>((true, product));
            }

            return new ValueTask<(bool, TProduct?)>((false, default));
        }

        /// <inheritdoc />
        public ValueTask<TProduct> CreateAsync(
            TKey key,
            CancellationToken cancellationToken = default)
        {
            try
            {
                return new ValueTask<TProduct>(_sync.Create(key));
            }
            catch (FactoryNotFoundException ex)
            {
                return new ValueTask<TProduct>(Task.FromException<TProduct>(ex));
            }
        }
    }
}
