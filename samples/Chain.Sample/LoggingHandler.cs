using DesignPatterns.Behavioral;

namespace Chain.Sample;

[HandlerOrder<RequestContext>(10)]
public sealed class LoggingHandler : IHandler<RequestContext>
{
    public async ValueTask InvokeAsync(
        RequestContext context,
        HandlerDelegate<RequestContext> next,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"[Log] Start {context.Path}");
        await next(context, cancellationToken);
        Console.WriteLine($"[Log] End {context.Path} -> {context.Response ?? "(no response)"}");
    }
}
