using System.Threading;
using System.Threading.Tasks;

namespace DesignPatterns.Behavioral;

/// <summary>
/// Handles events of type <typeparamref name="TEvent"/>.
/// </summary>
/// <typeparam name="TEvent">The event type to handle.</typeparam>
public interface IEventHandler<in TEvent>
{
    /// <summary>
    /// Handles the specified event.
    /// </summary>
    /// <param name="evt">The event instance.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    ValueTask HandleAsync(TEvent evt, CancellationToken cancellationToken = default);
}
