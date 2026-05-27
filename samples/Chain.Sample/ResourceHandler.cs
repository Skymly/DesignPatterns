using DesignPatterns.Behavioral;

namespace Chain.Sample;

[HandlerOrder<RequestContext>(30)]
public sealed class ResourceHandler : IHandler<RequestContext>
{
    public ValueTask InvokeAsync(
        RequestContext context,
        HandlerDelegate<RequestContext> next,
        CancellationToken cancellationToken = default)
    {
        context.Response = $"200 OK: {context.Path}";
        return default;
    }
}
