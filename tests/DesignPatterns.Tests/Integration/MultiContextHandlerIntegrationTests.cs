using DesignPatterns.Behavioral;

namespace DesignPatterns.Tests.Integration.Chain;

public sealed class MultiContextRequest
{
    public bool Handled { get; set; }
}

public sealed class MultiContextAudit
{
    public bool Handled { get; set; }
}

[HandlerOrder<MultiContextRequest>(10)]
[HandlerOrder<MultiContextAudit>(10)]
public sealed class MultiContextSharedHandler :
    IHandler<MultiContextRequest>,
    IHandler<MultiContextAudit>
{
    public ValueTask InvokeAsync(
        MultiContextRequest context,
        HandlerDelegate<MultiContextRequest> next,
        CancellationToken cancellationToken = default)
    {
        context.Handled = true;
        return next(context, cancellationToken);
    }

    public ValueTask InvokeAsync(
        MultiContextAudit context,
        HandlerDelegate<MultiContextAudit> next,
        CancellationToken cancellationToken = default)
    {
        context.Handled = true;
        return next(context, cancellationToken);
    }
}

public sealed class MultiContextHandlerIntegrationTests
{
    [Fact]
    public async Task SharedHandler_AppearsInBothGeneratedPipelines()
    {
        var request = new MultiContextRequest();
        await MultiContextRequestHandlerPipeline.Instance.InvokeAsync(request);
        Assert.True(request.Handled);

        var audit = new MultiContextAudit();
        await MultiContextAuditHandlerPipeline.Instance.InvokeAsync(audit);
        Assert.True(audit.Handled);
    }
}
