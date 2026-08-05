using System.Threading;
using System.Threading.Tasks;

namespace DesignPatterns.Behavioral;

/// <summary>
/// An immutable fork–join work graph that executes steps in topological waves.
/// </summary>
/// <remarks>
/// Same-wave steps may run concurrently against the same <typeparamref name="TContext"/> instance.
/// Overlapping unsynchronized writes to the context are forbidden; the library does not
/// isolate or merge context.
/// </remarks>
/// <typeparam name="TContext">The shared context mutated by steps.</typeparam>
public interface IWorkGraph<TContext>
{
    /// <summary>
    /// Runs the graph: steps in the same wave may execute concurrently; later waves
    /// wait until all predecessors complete. On the first step failure, in-flight peers
    /// are cancelled (cooperative via <see cref="CancellationToken"/>) and the failure
    /// is rethrown (fail-fast).
    /// </summary>
    /// <param name="context">The shared context passed to every step.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    ValueTask RunAsync(TContext context, CancellationToken cancellationToken = default);
}
