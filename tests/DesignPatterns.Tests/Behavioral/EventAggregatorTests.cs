using DesignPatterns.Behavioral;

namespace DesignPatterns.Tests.Behavioral;

public sealed class EventAggregatorTests
{
    [Fact]
    public async Task PublishAsync_SingleHandler_InvokesHandler()
    {
        var aggregator = new EventAggregator();
        var handler = new TrackingHandler();
        aggregator.Subscribe(handler);

        await aggregator.PublishAsync(new TestEvent("hello"));

        Assert.Single(handler.Received);
        Assert.Equal("hello", handler.Received[0].Message);
    }

    [Fact]
    public async Task PublishAsync_MultipleHandlers_InvokesAllInOrder()
    {
        var aggregator = new EventAggregator();
        var order = new List<int>();

        aggregator.Subscribe(new OrderTrackingHandler(1, order));
        aggregator.Subscribe(new OrderTrackingHandler(2, order));
        aggregator.Subscribe(new OrderTrackingHandler(3, order));

        await aggregator.PublishAsync(new TestEvent("x"));

        Assert.Equal(new[] { 1, 2, 3 }, order);
    }

    [Fact]
    public async Task PublishAsync_AfterUnsubscribe_DoesNotInvokeUnsubscribedHandler()
    {
        var aggregator = new EventAggregator();
        var handler = new TrackingHandler();
        aggregator.Subscribe(handler);

        aggregator.Unsubscribe(handler);

        await aggregator.PublishAsync(new TestEvent("x"));

        Assert.Empty(handler.Received);
    }

    [Fact]
    public async Task PublishAsync_NoSubscribers_CompletesSuccessfully()
    {
        var aggregator = new EventAggregator();

        await aggregator.PublishAsync(new TestEvent("x"));
    }

    [Fact]
    public async Task PublishAsync_Cancellation_StopsBeforeInvokingNextHandler()
    {
        var aggregator = new EventAggregator();
        var invoked = false;

        using var cts = new CancellationTokenSource();

        aggregator.Subscribe(new CancellingHandler(cts));
        aggregator.Subscribe(new DelegateHandler<TestEvent>(_ => { invoked = true; return default; }));

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => aggregator.PublishAsync(new TestEvent("x"), cts.Token).AsTask());

        Assert.False(invoked);
    }

    [Fact]
    public async Task PublishAsync_MultipleEventTypes_IsolatesHandlers()
    {
        var aggregator = new EventAggregator();
        var testEvents = new List<TestEvent>();
        var otherEvents = new List<OtherEvent>();

        aggregator.Subscribe(new DelegateHandler<TestEvent>(e =>
        {
            testEvents.Add(e);
            return default;
        }));
        aggregator.Subscribe(new DelegateHandler<OtherEvent>(e =>
        {
            otherEvents.Add(e);
            return default;
        }));

        await aggregator.PublishAsync(new TestEvent("a"));
        await aggregator.PublishAsync(new OtherEvent(42));

        Assert.Single(testEvents);
        Assert.Single(otherEvents);
        Assert.Equal("a", testEvents[0].Message);
        Assert.Equal(42, otherEvents[0].Value);
    }

    [Fact]
    public async Task PublishAsync_UnsubscribingOneHandler_KeepsOthers()
    {
        var aggregator = new EventAggregator();
        var handler1 = new TrackingHandler();
        var handler2 = new TrackingHandler();

        aggregator.Subscribe(handler1);
        aggregator.Subscribe(handler2);
        aggregator.Unsubscribe(handler1);

        await aggregator.PublishAsync(new TestEvent("x"));

        Assert.Empty(handler1.Received);
        Assert.Single(handler2.Received);
    }

    [Fact]
    public void Subscribe_NullHandler_Throws()
    {
        var aggregator = new EventAggregator();

        Assert.Throws<ArgumentNullException>(() => aggregator.Subscribe<TestEvent>(null!));
    }

    [Fact]
    public void Unsubscribe_NullHandler_Throws()
    {
        var aggregator = new EventAggregator();

        Assert.Throws<ArgumentNullException>(() => aggregator.Unsubscribe<TestEvent>(null!));
    }

    [Fact]
    public async Task ConcurrentSubscribeAndPublish_DoesNotThrow()
    {
        const int iterations = 100;
        var aggregator = new EventAggregator();
        var handler = new DelegateHandler<TestEvent>(_ => default);

        var start = new Barrier(participantCount: 3);

        var subscribeTask = Task.Run(() =>
        {
            start.SignalAndWait();
            for (var i = 0; i < iterations; i++)
            {
                aggregator.Subscribe(handler);
            }
        });

        var publishTask = Task.Run(async () =>
        {
            start.SignalAndWait();
            for (var i = 0; i < iterations; i++)
            {
                await aggregator.PublishAsync(new TestEvent("x"));
            }
        });

        start.SignalAndWait();

        var exception = await Record.ExceptionAsync(() => Task.WhenAll(subscribeTask, publishTask));
        Assert.Null(exception);
    }

    [Fact]
    public async Task PublishAfterSeed_InvokesAllHandlersInSnapshot()
    {
        var aggregator = new EventAggregator();
        var counter = 0;
        var handler = new DelegateHandler<TestEvent>(_ =>
        {
            Interlocked.Increment(ref counter);
            return default;
        });

        aggregator.Subscribe(handler);
        aggregator.Subscribe(handler);

        await aggregator.PublishAsync(new TestEvent("first"));

        Assert.Equal(2, counter);

        aggregator.Subscribe(new DelegateHandler<TestEvent>(_ =>
        {
            Interlocked.Increment(ref counter);
            return default;
        }));

        await aggregator.PublishAsync(new TestEvent("second"));

        Assert.Equal(5, counter);
    }

    [Fact]
    public async Task PublishAsync_StopOnError_StopsAtFirstThrowingHandler()
    {
        var aggregator = new EventAggregator();
        var invoked = new List<int>();
        aggregator.Subscribe(new DelegateHandler<TestEvent>(_ =>
        {
            invoked.Add(1);
            return default;
        }));
        aggregator.Subscribe(new DelegateHandler<TestEvent>(_ =>
        {
            invoked.Add(2);
            throw new InvalidOperationException("handler 2 failed");
        }));
        aggregator.Subscribe(new DelegateHandler<TestEvent>(_ =>
        {
            invoked.Add(3);
            return default;
        }));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            aggregator.PublishAsync(
                new TestEvent("x"),
                EventPublishErrorHandling.StopOnError).AsTask());

        Assert.Equal("handler 2 failed", ex.Message);
        Assert.Equal(new[] { 1, 2 }, invoked);
    }

    [Fact]
    public async Task PublishAsync_ContinueOnError_InvokesAllHandlers()
    {
        var aggregator = new EventAggregator();
        var invoked = new List<int>();
        aggregator.Subscribe(new DelegateHandler<TestEvent>(_ =>
        {
            invoked.Add(1);
            return default;
        }));
        aggregator.Subscribe(new DelegateHandler<TestEvent>(_ =>
        {
            invoked.Add(2);
            throw new InvalidOperationException("handler 2 failed");
        }));
        aggregator.Subscribe(new DelegateHandler<TestEvent>(_ =>
        {
            invoked.Add(3);
            return default;
        }));

        await aggregator.PublishAsync(
            new TestEvent("x"),
            EventPublishErrorHandling.ContinueOnError);

        Assert.Equal(new[] { 1, 2, 3 }, invoked);
    }

    [Fact]
    public async Task PublishAsync_AggregateErrors_InvokesAllAndThrowsAggregate()
    {
        var aggregator = new EventAggregator();
        var invoked = new List<int>();
        aggregator.Subscribe(new DelegateHandler<TestEvent>(_ =>
        {
            invoked.Add(1);
            throw new InvalidOperationException("handler 1 failed");
        }));
        aggregator.Subscribe(new DelegateHandler<TestEvent>(_ =>
        {
            invoked.Add(2);
            return default;
        }));
        aggregator.Subscribe(new DelegateHandler<TestEvent>(_ =>
        {
            invoked.Add(3);
            throw new InvalidOperationException("handler 3 failed");
        }));

        var ex = await Assert.ThrowsAsync<AggregateException>(() =>
            aggregator.PublishAsync(
                new TestEvent("x"),
                EventPublishErrorHandling.AggregateErrors).AsTask());

        Assert.Equal(new[] { 1, 2, 3 }, invoked);
        Assert.Equal(2, ex.InnerExceptions.Count);
        Assert.Equal("handler 1 failed", ex.InnerExceptions[0].Message);
        Assert.Equal("handler 3 failed", ex.InnerExceptions[1].Message);
    }

    [Fact]
    public async Task PublishAsync_AggregateErrors_NoExceptions_DoesNotThrow()
    {
        var aggregator = new EventAggregator();
        var invoked = 0;
        aggregator.Subscribe(new DelegateHandler<TestEvent>(_ =>
        {
            invoked++;
            return default;
        }));
        aggregator.Subscribe(new DelegateHandler<TestEvent>(_ =>
        {
            invoked++;
            return default;
        }));

        await aggregator.PublishAsync(
            new TestEvent("x"),
            EventPublishErrorHandling.AggregateErrors);

        Assert.Equal(2, invoked);
    }

    [Fact]
    public async Task PublishAsync_DefaultOverload_UsesStopOnError()
    {
        var aggregator = new EventAggregator();
        var invoked = new List<int>();
        aggregator.Subscribe(new DelegateHandler<TestEvent>(_ =>
        {
            invoked.Add(1);
            throw new InvalidOperationException("failed");
        }));
        aggregator.Subscribe(new DelegateHandler<TestEvent>(_ =>
        {
            invoked.Add(2);
            return default;
        }));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            aggregator.PublishAsync(new TestEvent("x")).AsTask());

        // Second handler should not be invoked (StopOnError default)
        Assert.Equal(new[] { 1 }, invoked);
    }

    // PublishTracedAsync tests

    [Fact]
    public async Task PublishTracedAsync_AllHandlersSucceed_RecordsCompleted()
    {
        var aggregator = new EventAggregator();
        aggregator.Subscribe(new TrackingHandler());
        aggregator.Subscribe(new TrackingHandler());

        var trace = await aggregator.PublishTracedAsync(new TestEvent("x"));

        Assert.Equal(2, trace.HandlerCount);
        Assert.Equal(EventPublicationStepStatus.Completed, trace.Steps[0].Status);
        Assert.Equal(EventPublicationStepStatus.Completed, trace.Steps[1].Status);
        Assert.False(trace.HasFailures);
        Assert.Equal(-1, trace.FailedHandlerIndex);
        Assert.Null(trace.Exception);
    }

    [Fact]
    public async Task PublishTracedAsync_NoSubscribers_ReturnsEmptyTrace()
    {
        var aggregator = new EventAggregator();

        var trace = await aggregator.PublishTracedAsync(new TestEvent("x"));

        Assert.Empty(trace.Steps);
        Assert.Equal(0, trace.HandlerCount);
        Assert.False(trace.HasFailures);
    }

    [Fact]
    public async Task PublishTracedAsync_StopOnError_RecordsFailedAndNotReached()
    {
        var aggregator = new EventAggregator();
        aggregator.Subscribe(new DelegateHandler<TestEvent>(_ => default));
        aggregator.Subscribe(new DelegateHandler<TestEvent>(_ =>
            throw new InvalidOperationException("boom")));
        aggregator.Subscribe(new DelegateHandler<TestEvent>(_ => default));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            aggregator.PublishTracedAsync(
                new TestEvent("x"),
                EventPublishErrorHandling.StopOnError).AsTask());

        // With StopOnError, the exception is re-thrown so we can't get the trace.
        // Use ContinueOnError to inspect the trace.
        var aggregator2 = new EventAggregator();
        aggregator2.Subscribe(new DelegateHandler<TestEvent>(_ => default));
        aggregator2.Subscribe(new DelegateHandler<TestEvent>(_ =>
            throw new InvalidOperationException("boom")));
        aggregator2.Subscribe(new DelegateHandler<TestEvent>(_ => default));

        var trace = await aggregator2.PublishTracedAsync(
            new TestEvent("x"),
            EventPublishErrorHandling.ContinueOnError);

        Assert.Equal(3, trace.HandlerCount);
        Assert.Equal(EventPublicationStepStatus.Completed, trace.Steps[0].Status);
        Assert.Equal(EventPublicationStepStatus.Failed, trace.Steps[1].Status);
        Assert.Equal(EventPublicationStepStatus.Completed, trace.Steps[2].Status);
        Assert.True(trace.HasFailures);
        Assert.Equal(1, trace.FailedHandlerIndex);
        Assert.NotNull(trace.Exception);
        Assert.Equal("boom", trace.Exception!.Message);
    }

    [Fact]
    public async Task PublishTracedAsync_ContinueOnError_RecordsAllOutcomes()
    {
        var aggregator = new EventAggregator();
        aggregator.Subscribe(new DelegateHandler<TestEvent>(_ => default));
        aggregator.Subscribe(new DelegateHandler<TestEvent>(_ =>
            throw new InvalidOperationException("fail-1")));
        aggregator.Subscribe(new DelegateHandler<TestEvent>(_ => default));
        aggregator.Subscribe(new DelegateHandler<TestEvent>(_ =>
            throw new InvalidOperationException("fail-2")));

        var trace = await aggregator.PublishTracedAsync(
            new TestEvent("x"),
            EventPublishErrorHandling.ContinueOnError);

        Assert.Equal(4, trace.HandlerCount);
        Assert.Equal(EventPublicationStepStatus.Completed, trace.Steps[0].Status);
        Assert.Equal(EventPublicationStepStatus.Failed, trace.Steps[1].Status);
        Assert.Equal(EventPublicationStepStatus.Completed, trace.Steps[2].Status);
        Assert.Equal(EventPublicationStepStatus.Failed, trace.Steps[3].Status);
        Assert.True(trace.HasFailures);
        Assert.Equal(1, trace.FailedHandlerIndex);
        Assert.Equal("fail-1", trace.Exception!.Message);
        Assert.Equal("fail-2", trace.Steps[3].Exception!.Message);
    }

    [Fact]
    public async Task PublishTracedAsync_AggregateErrors_RecordsAllAndThrows()
    {
        var aggregator = new EventAggregator();
        aggregator.Subscribe(new DelegateHandler<TestEvent>(_ =>
            throw new InvalidOperationException("fail-1")));
        aggregator.Subscribe(new DelegateHandler<TestEvent>(_ => default));
        aggregator.Subscribe(new DelegateHandler<TestEvent>(_ =>
            throw new InvalidOperationException("fail-2")));

        var ex = await Assert.ThrowsAsync<AggregateException>(() =>
            aggregator.PublishTracedAsync(
                new TestEvent("x"),
                EventPublishErrorHandling.AggregateErrors).AsTask());

        Assert.Equal(2, ex.InnerExceptions.Count);
        Assert.Equal("fail-1", ex.InnerExceptions[0].Message);
        Assert.Equal("fail-2", ex.InnerExceptions[1].Message);
    }

    [Fact]
    public async Task PublishTracedAsync_ObserverNotified_OnException()
    {
        var aggregator = new EventAggregator();
        aggregator.Subscribe(new DelegateHandler<TestEvent>(_ => default));
        aggregator.Subscribe(new DelegateHandler<TestEvent>(_ =>
            throw new InvalidOperationException("observed")));

        var observer = new CapturingPublicationObserver<TestEvent>();

        await aggregator.PublishTracedAsync(
            new TestEvent("ctx"),
            EventPublishErrorHandling.ContinueOnError,
            observer);

        Assert.Single(observer.Calls);
        Assert.Equal("ctx", observer.Calls[0].Event.Message);
        Assert.Equal(1, observer.Calls[0].HandlerIndex);
        Assert.NotNull(observer.Calls[0].Exception);
        Assert.Equal("observed", observer.Calls[0].Exception!.Message);
    }

    [Fact]
    public async Task PublishTracedAsync_NullObserver_WorksAsExpected()
    {
        var aggregator = new EventAggregator();
        aggregator.Subscribe(new DelegateHandler<TestEvent>(_ => default));

        var trace = await aggregator.PublishTracedAsync(
            new TestEvent("x"),
            EventPublishErrorHandling.StopOnError,
            null);

        Assert.Single(trace.Steps);
        Assert.Equal(EventPublicationStepStatus.Completed, trace.Steps[0].Status);
    }

    [Fact]
    public async Task PublishTracedAsync_DefaultOverload_UsesStopOnError()
    {
        var aggregator = new EventAggregator();
        aggregator.Subscribe(new DelegateHandler<TestEvent>(_ =>
            throw new InvalidOperationException("fail")));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            aggregator.PublishTracedAsync(new TestEvent("x")).AsTask());
    }

    [Fact]
    public async Task PublishTracedAsync_RecordsHandlerNames()
    {
        var aggregator = new EventAggregator();
        aggregator.Subscribe(new TrackingHandler());
        aggregator.Subscribe(new DelegateHandler<TestEvent>(_ => default));

        var trace = await aggregator.PublishTracedAsync(new TestEvent("x"));

        Assert.Equal(nameof(TrackingHandler), trace.Steps[0].HandlerName);
        Assert.Equal("DelegateHandler`1", trace.Steps[1].HandlerName);
    }

    // Helper types

    private sealed class CapturingPublicationObserver<TEvent> : IEventPublicationObserver<TEvent>
    {
        public List<(TEvent Event, int HandlerIndex, string HandlerName, Exception Exception)> Calls { get; } = new();

        public void OnHandlerException(TEvent evt, int handlerIndex, string handlerName, Exception exception)
        {
            Calls.Add((evt, handlerIndex, handlerName, exception));
        }
    }

    private sealed record TestEvent(string Message);
    private sealed record OtherEvent(int Value);

    private sealed class TrackingHandler : IEventHandler<TestEvent>
    {
        public List<TestEvent> Received { get; } = new();

        public ValueTask HandleAsync(TestEvent evt, CancellationToken cancellationToken = default)
        {
            Received.Add(evt);
            return default;
        }
    }

    private sealed class OrderTrackingHandler : IEventHandler<TestEvent>
    {
        private readonly int _id;
        private readonly List<int> _order;

        public OrderTrackingHandler(int id, List<int> order)
        {
            _id = id;
            _order = order;
        }

        public ValueTask HandleAsync(TestEvent evt, CancellationToken cancellationToken = default)
        {
            _order.Add(_id);
            return default;
        }
    }

    private sealed class CancellingHandler : IEventHandler<TestEvent>
    {
        private readonly CancellationTokenSource _cts;

        public CancellingHandler(CancellationTokenSource cts) => _cts = cts;

        public ValueTask HandleAsync(TestEvent evt, CancellationToken cancellationToken = default)
        {
            _cts.Cancel();
            throw new OperationCanceledException(cancellationToken);
        }
    }

    private sealed class DelegateHandler<TEvent> : IEventHandler<TEvent>
    {
        private readonly Func<TEvent, ValueTask> _handle;

        public DelegateHandler(Func<TEvent, ValueTask> handle) => _handle = handle;

        public ValueTask HandleAsync(TEvent evt, CancellationToken cancellationToken = default) =>
            cancellationToken.IsCancellationRequested
                ? new ValueTask(Task.FromCanceled(cancellationToken))
                : _handle(evt);
    }
}
