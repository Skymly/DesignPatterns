using DesignPatterns.Behavioral;

namespace Chain.Sample;

[HandlerOrder<RequestContext>(20)]
public sealed class AuthorizationHandler : IHandler<RequestContext>
{
    public ValueTask InvokeAsync(
        RequestContext context,
        HandlerDelegate<RequestContext> next,
        CancellationToken cancellationToken = default)
    {
        if (!context.IsAuthenticated)
        {
            context.Response = "401 Unauthorized";
            return default;
        }

        return next(context, cancellationToken);
    }
}
