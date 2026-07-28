using System.Threading;
using System.Threading.Tasks;

namespace DesignPatterns.Behavioral;

/// <summary>
/// Invokes the next stage in a void-style command pipeline onion (behavior or terminal handler).
/// </summary>
/// <typeparam name="TCommand">The command type flowing through the pipeline.</typeparam>
/// <param name="command">The command instance.</param>
/// <param name="cancellationToken">A cancellation token.</param>
public delegate ValueTask CommandPipelineDelegate<TCommand>(
    TCommand command,
    CancellationToken cancellationToken = default);
