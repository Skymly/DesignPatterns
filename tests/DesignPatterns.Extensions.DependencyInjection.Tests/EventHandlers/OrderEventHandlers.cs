using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DesignPatterns.Behavioral;

namespace DesignPatterns.Extensions.DependencyInjection.Tests.EventHandlers;

public record OrderPlacedEvent(string OrderId);

public sealed class HandledEventsCollector
{
    public List<string> Events { get; } = new();
}

[RegisterEventHandler<OrderPlacedEvent>]
public sealed class LogOrderHandler : IEventHandler<OrderPlacedEvent>
{
    private readonly HandledEventsCollector _collector;

    public LogOrderHandler(HandledEventsCollector collector) => _collector = collector;

    public ValueTask HandleAsync(OrderPlacedEvent evt, CancellationToken cancellationToken = default)
    {
        _collector.Events.Add($"Log:{evt.OrderId}");
        return default;
    }
}

[RegisterEventHandler<OrderPlacedEvent>]
public sealed class NotifyOrderHandler : IEventHandler<OrderPlacedEvent>
{
    private readonly HandledEventsCollector _collector;

    public NotifyOrderHandler(HandledEventsCollector collector) => _collector = collector;

    public ValueTask HandleAsync(OrderPlacedEvent evt, CancellationToken cancellationToken = default)
    {
        _collector.Events.Add($"Notify:{evt.OrderId}");
        return default;
    }
}
