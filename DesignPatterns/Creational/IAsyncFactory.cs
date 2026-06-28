using System.Threading;
using System.Threading.Tasks;

namespace DesignPatterns.Creational;

/// <summary>
/// Optional factory contract for asynchronous product creation.
/// </summary>
/// <typeparam name="TProduct">Product type created by the factory.</typeparam>
public interface IAsyncFactory<TProduct>
{
    /// <summary>
    /// Creates a product instance asynchronously.
    /// </summary>
    /// <param name="cancellationToken">Token to observe for cancellation.</param>
    /// <returns>A new product instance.</returns>
    ValueTask<TProduct> CreateAsync(CancellationToken cancellationToken = default);
}
