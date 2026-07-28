using System.Threading;
using System.Threading.Tasks;

namespace DesignPatterns.Behavioral;

/// <summary>
/// A pipeline behavior around a void-style <see cref="ICommandHandler{TCommand}"/>.
/// Call <paramref name="next"/> to continue the onion; omit the call to short-circuit.
/// </summary>
/// <typeparam name="TCommand">The command type this behavior applies to.</typeparam>
public interface ICommandPipelineBehavior<TCommand>
{
    /// <summary>
    /// Invokes the behavior for <paramref name="command"/>, optionally forwarding to <paramref name="next"/>.
    /// </summary>
    /// <param name="command">The command instance.</param>
    /// <param name="next">The next inbound stage (another behavior or the terminal handler).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    ValueTask InvokeAsync(
        TCommand command,
        CommandPipelineDelegate<TCommand> next,
        CancellationToken cancellationToken = default);
}
