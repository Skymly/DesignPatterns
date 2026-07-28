using System.Threading;
using System.Threading.Tasks;

namespace DesignPatterns.Behavioral;

/// <summary>
/// Invokes the next stage in a result-producing command pipeline onion (behavior or terminal handler).
/// </summary>
/// <typeparam name="TCommand">The command type flowing through the pipeline.</typeparam>
/// <typeparam name="TResult">The result type produced by the pipeline.</typeparam>
/// <param name="command">The command instance.</param>
/// <param name="cancellationToken">A cancellation token.</param>
/// <returns>The pipeline result.</returns>
public delegate ValueTask<TResult> CommandPipelineDelegate<TCommand, TResult>(
    TCommand command,
    CancellationToken cancellationToken = default);
