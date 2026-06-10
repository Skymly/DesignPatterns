using DesignPatterns.Behavioral;

namespace DesignPatterns.Tests.Behavioral;

public sealed class HandlerPipelineTests
{
    [Fact]
    public async Task InvokeAsync_RunsHandlersInRegistrationOrder()
    {
        var log = new List<string>();

        var pipeline = new HandlerPipelineBuilder<string>()
            .Use(async (context, next, _) =>
            {
                log.Add($"A:before:{context}");
                await next(context);
                log.Add($"A:after:{context}");
            })
            .Use(async (context, next, _) =>
            {
                log.Add($"B:before:{context}");
                await next(context);
                log.Add($"B:after:{context}");
            })
            .Use(async (context, next, _) =>
            {
                log.Add($"C:before:{context}");
                await next(context);
                log.Add($"C:after:{context}");
            })
            .Build();

        await pipeline.InvokeAsync("req");

        Assert.Equal(
            new[]
            {
                "A:before:req",
                "B:before:req",
                "C:before:req",
                "C:after:req",
                "B:after:req",
                "A:after:req",
            },
            log);
    }

    [Fact]
    public async Task InvokeAsync_WhenNextNotCalled_ShortCircuitsRemainingHandlers()
    {
        var log = new List<string>();

        var pipeline = new HandlerPipelineBuilder<string>()
            .Use(async (context, next, _) =>
            {
                log.Add("A");
                await next(context);
                log.Add("A:end");
            })
            .Use((_, _, _) =>
            {
                log.Add("B:blocked");
                return default;
            })
            .Use(async (context, next, _) =>
            {
                log.Add("C");
                await next(context);
            })
            .Build();

        await pipeline.InvokeAsync("req");

        Assert.Equal(new[] { "A", "B:blocked", "A:end" }, log);
    }

    [Fact]
    public async Task InvokeAsync_PropagatesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        var token = cts.Token;
        CancellationToken? tokenInA = null;
        CancellationToken? tokenInB = null;
        CancellationToken? tokenInNext = null;

        var pipeline = new HandlerPipelineBuilder<int>()
            .Use(async (_, next, ct) =>
            {
                tokenInA = ct;
                await next(0, ct);
            })
            .Use((_, next, ct) =>
            {
                tokenInB = ct;
                tokenInNext = ct;
                return next(0, ct);
            })
            .Build();

        await pipeline.InvokeAsync(0, token);

        Assert.Equal(token, tokenInA);
        Assert.Equal(token, tokenInB);
        Assert.Equal(token, tokenInNext);
    }

    [Fact]
    public async Task InvokeAsync_EmptyPipeline_CompletesSuccessfully()
    {
        var pipeline = new HandlerPipelineBuilder<string>().Build();

        await pipeline.InvokeAsync("req");
    }

    [Fact]
    public void Use_NullHandler_Throws()
    {
        var builder = new HandlerPipelineBuilder<string>();

        Assert.Throws<ArgumentNullException>(() => builder.Use((IHandler<string>)null!));
    }

    [Fact]
    public void Use_NullDelegate_Throws()
    {
        var builder = new HandlerPipelineBuilder<string>();

        Assert.Throws<ArgumentNullException>(() =>
            builder.Use((Func<string, HandlerDelegate<string>, CancellationToken, ValueTask>)null!));
    }

    [Fact]
    public async Task InvokeAsync_SingleHandler_InvokesHandler()
    {
        var invoked = false;

        var pipeline = new HandlerPipelineBuilder<string>()
            .Use((_, next, _) =>
            {
                invoked = true;
                return next("req");
            })
            .Build();

        await pipeline.InvokeAsync("req");

        Assert.True(invoked);
    }

    [Fact]
    public async Task InvokeAsync_ClassBasedHandler_Works()
    {
        string? result = null;

        var pipeline = new HandlerPipelineBuilder<string>()
            .Use(new AppendHandler("!"))
            .Use((ctx, _, _) =>
            {
                result = ctx;
                return default;
            })
            .Build();

        await pipeline.InvokeAsync("hi");

        Assert.Equal("hi!", result);
    }

    [Fact]
    public async Task InvokeAsync_PassesCancellationTokenToHandler()
    {
        using var cts = new CancellationTokenSource();
        CancellationToken? captured = null;

        var pipeline = new HandlerPipelineBuilder<int>()
            .Use(new CaptureTokenHandler(token => captured = token))
            .Build();

        await pipeline.InvokeAsync(1, cts.Token);

        Assert.Equal(cts.Token, captured);
    }

    [Fact]
    public async Task InvokeTracedAsync_EmptyPipeline_ReturnsEmptyTrace()
    {
        var pipeline = new HandlerPipelineBuilder<string>().Build();

        var trace = await pipeline.InvokeTracedAsync("req");

        Assert.Empty(trace.Steps);
        Assert.False(trace.WasShortCircuited);
    }

    [Fact]
    public async Task InvokeTracedAsync_AllHandlersContinue_MarksCompleted()
    {
        var pipeline = new HandlerPipelineBuilder<string>()
            .Use(new AppendHandler("!"))
            .Use((ctx, next, _) => next(ctx))
            .Build();

        var trace = await pipeline.InvokeTracedAsync("hi");

        Assert.Equal(2, trace.Steps.Count);
        Assert.Equal(HandlerPipelineStepStatus.Completed, trace.Steps[0].Status);
        Assert.Equal(nameof(AppendHandler), trace.Steps[0].Name);
        Assert.Equal(HandlerPipelineStepStatus.Completed, trace.Steps[1].Status);
        Assert.Equal("<delegate>", trace.Steps[1].Name);
        Assert.False(trace.WasShortCircuited);
    }

    [Fact]
    public async Task InvokeTracedAsync_ShortCircuit_MarksRemainingNotReached()
    {
        var pipeline = new HandlerPipelineBuilder<string>()
            .Use(new AppendHandler("!"))
            .Use((_, _, _) => default)
            .Use((ctx, next, _) => next(ctx))
            .Build();

        var trace = await pipeline.InvokeTracedAsync("hi");

        Assert.Equal(3, trace.Steps.Count);
        Assert.Equal(HandlerPipelineStepStatus.Completed, trace.Steps[0].Status);
        Assert.Equal(HandlerPipelineStepStatus.ShortCircuited, trace.Steps[1].Status);
        Assert.Equal(HandlerPipelineStepStatus.NotReached, trace.Steps[2].Status);
        Assert.True(trace.WasShortCircuited);
    }

    [Fact]
    public async Task InvokeTracedAsync_MatchesInvokeAsyncBehavior()
    {
        string? tracedResult = null;
        string? plainResult = null;

        var tracedPipeline = new HandlerPipelineBuilder<string>()
            .Use(new AppendHandler("!"))
            .Use((ctx, _, _) =>
            {
                tracedResult = ctx;
                return default;
            })
            .Build();

        var plainPipeline = new HandlerPipelineBuilder<string>()
            .Use(new AppendHandler("!"))
            .Use((ctx, _, _) =>
            {
                plainResult = ctx;
                return default;
            })
            .Build();

        await tracedPipeline.InvokeTracedAsync("hi");
        await plainPipeline.InvokeAsync("hi");

        Assert.Equal("hi!", tracedResult);
        Assert.Equal(plainResult, tracedResult);
    }

    private sealed class AppendHandler : IHandler<string>
    {
        private readonly string _suffix;

        public AppendHandler(string suffix) => _suffix = suffix;

        public ValueTask InvokeAsync(
            string context,
            HandlerDelegate<string> next,
            CancellationToken cancellationToken = default) =>
            next(context + _suffix, cancellationToken);
    }

    private sealed class CaptureTokenHandler : IHandler<int>
    {
        private readonly Action<CancellationToken> _capture;

        public CaptureTokenHandler(Action<CancellationToken> capture) => _capture = capture;

        public ValueTask InvokeAsync(
            int context,
            HandlerDelegate<int> next,
            CancellationToken cancellationToken = default)
        {
            _capture(cancellationToken);
            return default;
        }
    }
}
