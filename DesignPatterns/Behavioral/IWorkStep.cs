using System.Threading;
using System.Threading.Tasks;

namespace DesignPatterns.Behavioral;

/// <summary>
/// A single step in a fork–join work graph. Steps share one <typeparamref name="TContext"/>
/// and declare readiness dependencies only (not typed payload channels).
/// </summary>
/// <remarks>
/// Same-wave steps may run concurrently against the same context instance.
/// Overlapping unsynchronized writes to <typeparamref name="TContext"/> are forbidden;
/// the library does not isolate or merge context.
/// </remarks>
/// <typeparam name="TContext">The shared context mutated by steps during <see cref="ExecuteAsync"/>.</typeparam>
public interface IWorkStep<TContext>
{
    /// <summary>
    /// Executes this step against the shared context.
    /// </summary>
    /// <param name="context">The shared graph context.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    ValueTask ExecuteAsync(TContext context, CancellationToken cancellationToken = default);
}
