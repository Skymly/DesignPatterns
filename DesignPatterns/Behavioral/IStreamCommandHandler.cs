using System.Collections.Generic;
using System.Threading;

namespace DesignPatterns.Behavioral;

/// <summary>
/// Handles commands of type <typeparamref name="TCommand"/> by producing a progressive stream of
/// <typeparamref name="TItem"/> values.
/// </summary>
/// <typeparam name="TCommand">The command type to handle.</typeparam>
/// <typeparam name="TItem">The item type yielded by the stream.</typeparam>
public interface IStreamCommandHandler<in TCommand, out TItem>
{
    /// <summary>
    /// Handles the specified command and yields progressive results.
    /// </summary>
    /// <param name="command">The command instance.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>An asynchronous stream of <typeparamref name="TItem"/> values.</returns>
    IAsyncEnumerable<TItem> HandleAsync(TCommand command, CancellationToken cancellationToken = default);
}
