using System.Threading;
using System.Threading.Tasks;

namespace DesignPatterns.Behavioral;

/// <summary>
/// Invokes the next handler in a <see cref="HandlerPipeline{TContext}"/>.
/// </summary>
/// <typeparam name="TContext">The context type flowing through the pipeline.</typeparam>
/// <param name="context">The current context.</param>
/// <param name="cancellationToken">A cancellation token.</param>
public delegate ValueTask HandlerDelegate<TContext>(
    TContext context,
    CancellationToken cancellationToken = default);
