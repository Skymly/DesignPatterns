using DesignPatterns.Behavioral;

namespace DesignPatterns.Tests.Integration.Chain;

public sealed class IntegrationRequestContext
{
    public IntegrationRequestContext(bool isAuthenticated)
    {
        IsAuthenticated = isAuthenticated;
    }

    public bool IsAuthenticated { get; }

    public string? Response { get; set; }
}

[HandlerOrder<IntegrationRequestContext>(10)]
public sealed class IntegrationLoggingHandler : IHandler<IntegrationRequestContext>
{
    public static int InvokeCount { get; set; }

    public ValueTask InvokeAsync(
        IntegrationRequestContext context,
        HandlerDelegate<IntegrationRequestContext> next,
        CancellationToken cancellationToken = default)
    {
        InvokeCount++;
        return next(context, cancellationToken);
    }
}

[HandlerOrder<IntegrationRequestContext>(20)]
public sealed class IntegrationAuthHandler : IHandler<IntegrationRequestContext>
{
    public ValueTask InvokeAsync(
        IntegrationRequestContext context,
        HandlerDelegate<IntegrationRequestContext> next,
        CancellationToken cancellationToken = default)
    {
        if (!context.IsAuthenticated)
        {
            context.Response = "401";
            return default;
        }

        return next(context, cancellationToken);
    }
}

[HandlerOrder<IntegrationRequestContext>(30)]
public sealed class IntegrationTerminalHandler : IHandler<IntegrationRequestContext>
{
    public ValueTask InvokeAsync(
        IntegrationRequestContext context,
        HandlerDelegate<IntegrationRequestContext> next,
        CancellationToken cancellationToken = default)
    {
        context.Response = "200";
        return default;
    }
}

public sealed class HandlerPipelineIntegrationTests
{
    [Fact]
    public async Task GeneratedPipeline_InvokesHandlersInOrder()
    {
        IntegrationLoggingHandler.InvokeCount = 0;
        var pipeline = IntegrationRequestContextHandlerPipeline.Instance;
        var context = new IntegrationRequestContext(isAuthenticated: true);

        await pipeline.InvokeAsync(context);

        Assert.Equal("200", context.Response);
        Assert.Equal(1, IntegrationLoggingHandler.InvokeCount);
    }

    [Fact]
    public async Task GeneratedPipeline_ShortCircuitsBeforeLaterHandlers()
    {
        IntegrationLoggingHandler.InvokeCount = 0;
        var pipeline = IntegrationRequestContextHandlerPipeline.Instance;
        var context = new IntegrationRequestContext(isAuthenticated: false);

        await pipeline.InvokeAsync(context);

        Assert.Equal("401", context.Response);
        Assert.Equal(1, IntegrationLoggingHandler.InvokeCount);
    }

    [Fact]
    public async Task GeneratedPipeline_InvokeTracedAsync_AllHandlersRun_TerminalDoesNotCallNext()
    {
        IntegrationLoggingHandler.InvokeCount = 0;
        var pipeline = IntegrationRequestContextHandlerPipeline.Instance;
        var context = new IntegrationRequestContext(isAuthenticated: true);

        var trace = await pipeline.InvokeTracedAsync(context);

        Assert.Equal("200", context.Response);
        Assert.Equal(1, IntegrationLoggingHandler.InvokeCount);
        Assert.Equal(3, trace.Steps.Count);
        Assert.Equal(HandlerPipelineStepStatus.Completed, trace.Steps[0].Status);
        Assert.Equal(nameof(IntegrationLoggingHandler), trace.Steps[0].Name);
        Assert.Equal(HandlerPipelineStepStatus.Completed, trace.Steps[1].Status);
        Assert.Equal(nameof(IntegrationAuthHandler), trace.Steps[1].Name);
        Assert.Equal(HandlerPipelineStepStatus.ShortCircuited, trace.Steps[2].Status);
        Assert.Equal(nameof(IntegrationTerminalHandler), trace.Steps[2].Name);
        Assert.NotEqual(HandlerPipelineStepStatus.NotReached, trace.Steps[2].Status);
    }

    [Fact]
    public async Task GeneratedPipeline_InvokeTracedAsync_ShortCircuit_MarksTerminalNotReached()
    {
        IntegrationLoggingHandler.InvokeCount = 0;
        var pipeline = IntegrationRequestContextHandlerPipeline.Instance;
        var context = new IntegrationRequestContext(isAuthenticated: false);

        var trace = await pipeline.InvokeTracedAsync(context);

        Assert.Equal("401", context.Response);
        Assert.Equal(1, IntegrationLoggingHandler.InvokeCount);
        Assert.Equal(3, trace.Steps.Count);
        Assert.True(trace.WasShortCircuited);
        Assert.Equal(HandlerPipelineStepStatus.Completed, trace.Steps[0].Status);
        Assert.Equal(nameof(IntegrationLoggingHandler), trace.Steps[0].Name);
        Assert.Equal(HandlerPipelineStepStatus.ShortCircuited, trace.Steps[1].Status);
        Assert.Equal(nameof(IntegrationAuthHandler), trace.Steps[1].Name);
        Assert.Equal(HandlerPipelineStepStatus.NotReached, trace.Steps[2].Status);
        Assert.Equal(nameof(IntegrationTerminalHandler), trace.Steps[2].Name);
    }
}
