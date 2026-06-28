using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

namespace DesignPatterns.Creational;

/// <summary>
/// Read-only registry that creates product instances by key using registered async factories.
/// Each <see cref="CreateAsync"/> or successful <see cref="TryCreateAsync"/> invokes the
/// factory delegate (new instance per call).
/// </summary>
/// <typeparam name="TKey">Key type.</typeparam>
/// <typeparam name="TProduct">Product type created by factories.</typeparam>
public interface IAsyncFactoryRegistry<TKey, TProduct> : IReadOnlyRegistry<TKey, TProduct>
    where TKey : notnull
{
    /// <summary>
    /// Tries to create a product for the given key asynchronously.
    /// </summary>
    /// <param name="key">The factory key.</param>
    /// <param name="cancellationToken">Token to observe for cancellation.</param>
    /// <returns>
    /// A <see cref="ValueTask{TResult}"/> containing a tuple of success flag and the created
    /// product when successful.
    /// </returns>
    ValueTask<(bool Success, TProduct? Product)> TryCreateAsync(
        TKey key,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a product for the given key asynchronously.
    /// </summary>
    /// <param name="key">The factory key.</param>
    /// <param name="cancellationToken">Token to observe for cancellation.</param>
    /// <returns>A new product instance.</returns>
    /// <exception cref="FactoryNotFoundException">When the key is not registered.</exception>
    ValueTask<TProduct> CreateAsync(
        TKey key,
        CancellationToken cancellationToken = default);
}
