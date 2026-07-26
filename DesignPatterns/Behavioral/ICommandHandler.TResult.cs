using System.Threading;
using System.Threading.Tasks;

namespace DesignPatterns.Behavioral;

/// <summary>
/// Handles commands of type <typeparamref name="TCommand"/> and returns a <typeparamref name="TResult"/>.
/// </summary>
/// <typeparam name="TCommand">The command type to handle.</typeparam>
/// <typeparam name="TResult">The result type produced by the handler.</typeparam>
public interface ICommandHandler<in TCommand, TResult>
{
    /// <summary>
    /// Handles the specified command and returns a result.
    /// </summary>
    /// <param name="command">The command instance.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The handler result.</returns>
    ValueTask<TResult> HandleAsync(TCommand command, CancellationToken cancellationToken = default);
}
