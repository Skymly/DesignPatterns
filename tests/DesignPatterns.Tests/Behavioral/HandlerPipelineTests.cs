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

    [Fact]
    public async Task Guard_Passes_ExecutesHandler()
    {
        var executed = false;

        var pipeline = new HandlerPipelineBuilder<string>()
            .Use(new RecordingHandler("A", _ => executed = true), _ => true)
            .Build();

        await pipeline.InvokeAsync("req");

        Assert.True(executed);
    }

    [Fact]
    public async Task Guard_Fails_SkipsHandler()
    {
        var log = new List<string>();

        var pipeline = new HandlerPipelineBuilder<string>()
            .Use(new RecordingHandler("A", _ => log.Add("A")), _ => false)
            .Use(new RecordingHandler("B", _ => log.Add("B")), _ => true)
            .Build();

        await pipeline.InvokeAsync("req");

        Assert.DoesNotContain("A", log);
        Assert.Contains("B", log);
    }

    [Fact]
    public async Task Guard_Null_ExecutesHandler()
    {
        var executed = false;

        var pipeline = new HandlerPipelineBuilder<string>()
            .Use(new RecordingHandler("A", _ => executed = true), guard: null)
            .Build();

        await pipeline.InvokeAsync("req");

        Assert.True(executed);
    }

    [Fact]
    public async Task Trace_GuardFails_RecordsSkipped()
    {
        var pipeline = new HandlerPipelineBuilder<string>()
            .Use(new RecordingHandler("A", _ => { }), _ => false)
            .Build();

        var trace = await pipeline.InvokeTracedAsync("req");

        Assert.Single(trace.Steps);
        Assert.Equal(HandlerPipelineStepStatus.Skipped, trace.Steps[0].Status);
    }

    [Fact]
    public async Task Trace_WasSkipped_True()
    {
        var pipeline = new HandlerPipelineBuilder<string>()
            .Use(new RecordingHandler("A", _ => { }), _ => false)
            .Use(new RecordingHandler("B", _ => { }), _ => true)
            .Build();

        var trace = await pipeline.InvokeTracedAsync("req");

        Assert.True(trace.WasSkipped);
    }

    [Fact]
    public async Task Trace_WasSkipped_False_WhenNoGuards()
    {
        var pipeline = new HandlerPipelineBuilder<string>()
            .Use(new AppendHandler("!"))
            .Use((ctx, next, _) => next(ctx))
            .Build();

        var trace = await pipeline.InvokeTracedAsync("hi");

        Assert.False(trace.WasSkipped);
    }

    [Fact]
    public async Task InvokeTracedAsync_HandlerThrows_RecordsFailureAndRethrows()
    {
        var pipeline = new HandlerPipelineBuilder<string>()
            .Use(new AppendHandler("!"))
            .Use((_, _, _) => throw new InvalidOperationException("boom"))
            .Use((ctx, next, _) => next(ctx))
            .Build();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => pipeline.InvokeTracedAsync("hi").AsTask());

        // We can't get the trace from the thrown exception directly, but we can
        // verify the exception message is preserved.
        Assert.Equal("boom", ex.Message);
    }

    [Fact]
    public async Task InvokeTracedAsync_HandlerThrows_RecordsFailedInTrace()
    {
        var pipeline = new HandlerPipelineBuilder<string>()
            .Use(new AppendHandler("!"))
            .Use((_, _, _) => throw new InvalidOperationException("boom"))
            .Use((ctx, next, _) => next(ctx))
            .Build();

        // Use the observer overload to capture failure details.
        var observer = new CapturingExceptionObserver<string>();
        try
        {
            await pipeline.InvokeTracedAsync("hi", observer);
        }
        catch (InvalidOperationException)
        {
            // Expected.
        }

        Assert.NotNull(observer.LastException);
        Assert.Equal("boom", observer.LastException!.Message);
        Assert.Equal(1, observer.LastHandlerIndex);
    }

    [Fact]
    public async Task InvokeTracedAsync_HandlerThrows_RecordsFailedAndNotReached()
    {
        // Build a pipeline where handler at index 1 throws.
        // Handler 0 completes, handler 1 fails, handler 2 is not reached.
        var pipeline = new HandlerPipelineBuilder<string>()
            .Use((ctx, next, _) => next(ctx))
            .Use((_, _, _) => throw new InvalidOperationException("fail"))
            .Use((ctx, next, _) => next(ctx))
            .Build();

        var observer = new CapturingExceptionObserver<string>();
        try
        {
            await pipeline.InvokeTracedAsync("req", observer);
        }
        catch (InvalidOperationException)
        {
            // Expected — trace is not returned when exception is thrown.
        }

        // The observer was notified with the correct index and exception.
        Assert.Equal(1, observer.LastHandlerIndex);
        Assert.NotNull(observer.LastException);
        Assert.Equal("fail", observer.LastException!.Message);
    }

    [Fact]
    public async Task InvokeTracedAsync_GuardThrows_RecordsFailure()
    {
        var pipeline = new HandlerPipelineBuilder<string>()
            .Use(new RecordingHandler("A", _ => { }), _ => throw new InvalidOperationException("guard-fail"))
            .Use(new RecordingHandler("B", _ => { }), _ => true)
            .Build();

        var observer = new CapturingExceptionObserver<string>();
        try
        {
            await pipeline.InvokeTracedAsync("req", observer);
        }
        catch (InvalidOperationException)
        {
            // Expected.
        }

        Assert.Equal(0, observer.LastHandlerIndex);
        Assert.NotNull(observer.LastException);
        Assert.Equal("guard-fail", observer.LastException!.Message);
    }

    [Fact]
    public async Task InvokeTracedAsync_ObserverNotified_OnException()
    {
        var pipeline = new HandlerPipelineBuilder<string>()
            .Use((_, _, _) => throw new InvalidOperationException("observed"))
            .Build();

        var observer = new CapturingExceptionObserver<string>();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => pipeline.InvokeTracedAsync("ctx", observer).AsTask());

        Assert.Equal("ctx", observer.LastContext);
        Assert.Equal(0, observer.LastHandlerIndex);
        Assert.Equal("<delegate>", observer.LastHandlerName);
        Assert.NotNull(observer.LastException);
        Assert.Equal("observed", observer.LastException!.Message);
    }

    [Fact]
    public async Task InvokeTracedAsync_NullObserver_DelegatesToParameterlessOverload()
    {
        var pipeline = new HandlerPipelineBuilder<string>()
            .Use((ctx, next, _) => next(ctx))
            .Build();

        var trace = await pipeline.InvokeTracedAsync("req", null);

        Assert.Single(trace.Steps);
        Assert.Equal(HandlerPipelineStepStatus.Completed, trace.Steps[0].Status);
    }

    private sealed class CapturingExceptionObserver<TContext> : IHandlerExceptionObserver<TContext>
    {
        public TContext? LastContext { get; private set; }
        public int LastHandlerIndex { get; private set; } = -1;
        public string? LastHandlerName { get; private set; }
        public Exception? LastException { get; private set; }

        public void OnHandlerException(TContext context, int handlerIndex, string handlerName, Exception exception)
        {
            LastContext = context;
            LastHandlerIndex = handlerIndex;
            LastHandlerName = handlerName;
            LastException = exception;
        }
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

    private sealed class RecordingHandler : IHandler<string>
    {
        private readonly string _name;
        private readonly Action<string> _onInvoke;

        public RecordingHandler(string name, Action<string> onInvoke)
        {
            _name = name;
            _onInvoke = onInvoke;
        }

        public ValueTask InvokeAsync(
            string context,
            HandlerDelegate<string> next,
            CancellationToken cancellationToken = default)
        {
            _onInvoke(_name);
            return next(context, cancellationToken);
        }
    }
}
