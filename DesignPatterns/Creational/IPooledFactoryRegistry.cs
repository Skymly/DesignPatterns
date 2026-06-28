using System.Threading;
using System.Threading.Tasks;

namespace DesignPatterns.Creational;

/// <summary>
/// Pooled factory registry that reuses product instances via per-key pools.
/// Products are rented via <see cref="RentAsync"/> and returned via <see cref="Return"/>.
/// When the pool is empty, a new instance is created using the registered factory.
/// </summary>
/// <typeparam name="TKey">Key type.</typeparam>
/// <typeparam name="TProduct">Product type (must be a reference type).</typeparam>
public interface IPooledFactoryRegistry<TKey, TProduct> : IAsyncFactoryRegistry<TKey, TProduct>
    where TKey : notnull
    where TProduct : class
{
    /// <summary>
    /// Rents a product from the pool for the given key.
    /// When the pool has an available instance, it is returned immediately (synchronously).
    /// When the pool is empty, a new instance is created asynchronously via the factory.
    /// The product must be returned via <see cref="Return"/> when no longer needed.
    /// </summary>
    /// <param name="key">The factory key.</param>
    /// <param name="cancellationToken">Token to observe for cancellation.</param>
    /// <returns>A product instance (pooled or newly created).</returns>
    /// <exception cref="FactoryNotFoundException">When the key is not registered.</exception>
    ValueTask<TProduct> RentAsync(
        TKey key,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a product to the pool for reuse.
    /// If the product implements <see cref="IResettable"/>, <see cref="IResettable.Reset"/>
    /// is called before returning it to the pool.
    /// When the pool is full, the product is silently discarded.
    /// </summary>
    /// <param name="key">The factory key.</param>
    /// <param name="product">The product to return.</param>
    void Return(TKey key, TProduct product);
}
