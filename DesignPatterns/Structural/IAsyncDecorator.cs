using System.Threading;
using System.Threading.Tasks;

namespace DesignPatterns.Structural;

/// <summary>
/// Asynchronous variant of <see cref="IDecorator{TService}"/>.
/// Wraps an inner service instance with the same <typeparamref name="TService"/> contract
/// using an async pipeline (e.g. for I/O-bound decoration logic).
/// </summary>
/// <typeparam name="TService">The service contract type.</typeparam>
public interface IAsyncDecorator<TService>
    where TService : class
{
    /// <summary>
    /// Returns a new <typeparamref name="TService"/> that delegates to <paramref name="inner"/>.
    /// </summary>
    /// <param name="inner">The inner service instance.</param>
    /// <param name="cancellationToken">Token to observe for cancellation.</param>
    ValueTask<TService> DecorateAsync(TService inner, CancellationToken cancellationToken = default);
}
