using System.Threading;
using System.Threading.Tasks;

namespace DesignPatterns.Behavioral;

/// <summary>
/// Optional strategy contract for asynchronous algorithms.
/// </summary>
/// <typeparam name="TInput">Input type.</typeparam>
/// <typeparam name="TOutput">Output type.</typeparam>
public interface IAsyncStrategy<in TInput, TOutput>
{
    /// <summary>
    /// Executes the strategy asynchronously.
    /// </summary>
    ValueTask<TOutput> ExecuteAsync(TInput input, CancellationToken cancellationToken = default);
}
