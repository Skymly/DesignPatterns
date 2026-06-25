using System.Threading;
using System.Threading.Tasks;
using DesignPatterns.Behavioral;

namespace DesignPatterns.Extensions.DependencyInjection.Tests.EventHandlers;

public record SimpleEvent(string Message);

[RegisterEventHandler<SimpleEvent>]
public sealed class SimpleEventHandler : IEventHandler<SimpleEvent>
{
    public ValueTask HandleAsync(SimpleEvent evt, CancellationToken cancellationToken = default) =>
        default;
}
