using DesignPatterns.Behavioral;

namespace DesignPatterns.Extensions.Autofac.Tests.Handlers;

public sealed class RequestContext
{
    public string? Response { get; set; }
}

[HandlerOrder<RequestContext>(10)]
public sealed class LoggingHandler : IHandler<RequestContext>
{
    public ValueTask InvokeAsync(
        RequestContext context,
        HandlerDelegate<RequestContext> next,
        CancellationToken cancellationToken = default)
    {
        context.Response = "Logging";
        return next(context, cancellationToken);
    }
}

[HandlerOrder<RequestContext>(20)]
public sealed class AuthorizationHandler : IHandler<RequestContext>
{
    public ValueTask InvokeAsync(
        RequestContext context,
        HandlerDelegate<RequestContext> next,
        CancellationToken cancellationToken = default)
    {
        context.Response += ",Authorization";
        return next(context, cancellationToken);
    }
}
