using System.Threading;
using System.Threading.Tasks;

namespace DesignPatterns.Behavioral;

/// <summary>
/// A single stage in a chain-of-responsibility pipeline.
/// Call <paramref name="next"/> to forward the context; omit the call to short-circuit.
/// </summary>
/// <typeparam name="TContext">The context type flowing through the pipeline.</typeparam>
public interface IHandler<TContext>
{
    /// <summary>
    /// Handles the context and optionally invokes the next handler.
    /// </summary>
    /// <param name="context">The current context.</param>
    /// <param name="next">The next handler in the pipeline.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    ValueTask InvokeAsync(
        TContext context,
        HandlerDelegate<TContext> next,
        CancellationToken cancellationToken = default);
}
