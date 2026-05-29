using DesignPatterns.Behavioral;

var aggregator = new EventAggregator();

// Subscribe handlers
var emailHandler = new EmailNotificationHandler();
var auditHandler = new AuditLogHandler();
aggregator.Subscribe(emailHandler);
aggregator.Subscribe(auditHandler);

// Publish an order placed event
var orderEvent = new OrderPlacedEvent("ORD-001", 149.99m);
Console.WriteLine($"Publishing: OrderPlaced {{ OrderId = {orderEvent.OrderId}, Total = {orderEvent.Total:C} }}");
await aggregator.PublishAsync(orderEvent);
Console.WriteLine();

// Unsubscribe the audit handler and publish again
Console.WriteLine("Unsubscribing AuditLogHandler...");
aggregator.Unsubscribe(auditHandler);
Console.WriteLine();
await aggregator.PublishAsync(new OrderPlacedEvent("ORD-002", 59.99m));

// Demonstrate multiple event types
aggregator.Subscribe(new InventoryLowHandler());
Console.WriteLine();
Console.WriteLine($"Publishing: InventoryLowEvent {{ ProductId = \"SKU-42\", Remaining = 3 }}");
await aggregator.PublishAsync(new InventoryLowEvent("SKU-42", 3));

// --- Event and handler definitions ---

public sealed record OrderPlacedEvent(string OrderId, decimal Total);
public sealed record InventoryLowEvent(string ProductId, int Remaining);

public sealed class EmailNotificationHandler : IEventHandler<OrderPlacedEvent>
{
    public ValueTask HandleAsync(OrderPlacedEvent evt, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"  [Email] Sending confirmation for order {evt.OrderId} ({evt.Total:C})");
        return default;
    }
}

public sealed class AuditLogHandler : IEventHandler<OrderPlacedEvent>
{
    public ValueTask HandleAsync(OrderPlacedEvent evt, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"  [Audit] Logged order {evt.OrderId} at {DateTime.UtcNow:O}");
        return default;
    }
}

public sealed class InventoryLowHandler : IEventHandler<InventoryLowEvent>
{
    public ValueTask HandleAsync(InventoryLowEvent evt, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"  [Inventory] Alert: {evt.ProductId} has only {evt.Remaining} units remaining");
        return default;
    }
}
