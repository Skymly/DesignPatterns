using DesignPatterns.Behavioral;

namespace DependencyInjection.Sample;

public sealed class RequestContext
{
    public string? Trace { get; set; }
}

[HandlerOrder<RequestContext>(10)]
public sealed class LoggingHandler : IHandler<RequestContext>
{
    public ValueTask InvokeAsync(
        RequestContext context,
        HandlerDelegate<RequestContext> next,
        CancellationToken cancellationToken = default)
    {
        context.Trace = "Logging";
        return next(context, cancellationToken);
    }
}

[HandlerOrder<RequestContext>(20)]
public sealed class EnrichmentHandler : IHandler<RequestContext>
{
    public ValueTask InvokeAsync(
        RequestContext context,
        HandlerDelegate<RequestContext> next,
        CancellationToken cancellationToken = default)
    {
        context.Trace += " -> Enrichment";
        return next(context, cancellationToken);
    }
}
