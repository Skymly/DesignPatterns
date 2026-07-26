using System.Threading;
using System.Threading.Tasks;

namespace DesignPatterns.Behavioral;

/// <summary>
/// Handles commands of type <typeparamref name="TCommand"/> that do not produce a typed result.
/// </summary>
/// <typeparam name="TCommand">The command type to handle.</typeparam>
public interface ICommandHandler<in TCommand>
{
    /// <summary>
    /// Handles the specified command.
    /// </summary>
    /// <param name="command">The command instance.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    ValueTask HandleAsync(TCommand command, CancellationToken cancellationToken = default);
}
