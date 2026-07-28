using System.Threading;
using System.Threading.Tasks;

namespace DesignPatterns.Behavioral;

/// <summary>
/// A pipeline behavior around a result-producing <see cref="ICommandHandler{TCommand, TResult}"/>.
/// Return a value without calling <paramref name="next"/> to short-circuit, or
/// <c>return await next(...)</c> to continue the onion (MediatR-shaped).
/// </summary>
/// <typeparam name="TCommand">The command type this behavior applies to.</typeparam>
/// <typeparam name="TResult">The result type produced by the pipeline.</typeparam>
public interface ICommandPipelineBehavior<TCommand, TResult>
{
    /// <summary>
    /// Invokes the behavior for <paramref name="command"/>, optionally forwarding to <paramref name="next"/>.
    /// </summary>
    /// <param name="command">The command instance.</param>
    /// <param name="next">The next inbound stage (another behavior or the terminal handler).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The short-circuit result or the result of <paramref name="next"/>.</returns>
    ValueTask<TResult> InvokeAsync(
        TCommand command,
        CommandPipelineDelegate<TCommand, TResult> next,
        CancellationToken cancellationToken = default);
}
