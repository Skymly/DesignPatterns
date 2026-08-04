using DesignPatterns.Behavioral;

namespace DesignPatterns.Tests.Behavioral;

public sealed class WorkGraphTests
{
    [Fact]
    public void Build_WhenEmpty_Throws()
    {
        var builder = new WorkGraphBuilder<WorkContext>();

        var ex = Assert.Throws<InvalidWorkGraphException>(() => builder.Build());
        Assert.Contains("empty", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunAsync_SingleNode_ExecutesStep()
    {
        var log = new List<string>();
        var graph = new WorkGraphBuilder<WorkContext>()
            .Add("only", new RecordingStep("only", log))
            .Build();

        await graph.RunAsync(new WorkContext(), CancellationToken.None);

        Assert.Equal(new[] { "only" }, log);
    }

    [Fact]
    public async Task RunAsync_Diamond_RunsPredecessorsBeforeJoin()
    {
        var log = new List<string>();
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var graph = new WorkGraphBuilder<WorkContext>()
            .Add("A", new RecordingStep("A", log, onExecute: () =>
            {
                gate.TrySetResult(true);
                return Task.CompletedTask;
            }))
            .Add("B", new RecordingStep("B", log, onExecute: async () =>
            {
                // Ensure A can start in the same wave before B finishes recording.
                await gate.Task.WaitAsync(TimeSpan.FromSeconds(5));
            }))
            .Add("C", new RecordingStep("C", log), "A", "B")
            .Build();

        await graph.RunAsync(new WorkContext(), CancellationToken.None);

        Assert.Contains("A", log);
        Assert.Contains("B", log);
        Assert.Equal("C", log[^1]);
        Assert.Equal(3, log.Count);
    }

    [Fact]
    public void Build_WhenDuplicateId_Throws()
    {
        var builder = new WorkGraphBuilder<WorkContext>()
            .Add("A", new NoOpStep())
            .Add("A", new NoOpStep());

        var ex = Assert.Throws<InvalidWorkGraphException>(() => builder.Build());
        Assert.Contains("A", ex.Message, StringComparison.Ordinal);
        Assert.Contains("duplicate", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Build_WhenSelfDependency_Throws()
    {
        var builder = new WorkGraphBuilder<WorkContext>()
            .Add("A", new NoOpStep(), "A");

        var ex = Assert.Throws<InvalidWorkGraphException>(() => builder.Build());
        Assert.Contains("A", ex.Message, StringComparison.Ordinal);
        Assert.Contains("self", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Build_WhenUnknownDependsOn_Throws()
    {
        var builder = new WorkGraphBuilder<WorkContext>()
            .Add("A", new NoOpStep(), "missing");

        var ex = Assert.Throws<InvalidWorkGraphException>(() => builder.Build());
        Assert.Contains("missing", ex.Message, StringComparison.Ordinal);
        Assert.Contains("A", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_WhenCycle_Throws()
    {
        var builder = new WorkGraphBuilder<WorkContext>()
            .Add("A", new NoOpStep(), "B")
            .Add("B", new NoOpStep(), "A");

        var ex = Assert.Throws<InvalidWorkGraphException>(() => builder.Build());
        Assert.Contains("cycle", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunAsync_WhenStepFails_CancelsInFlightPeersAndThrows()
    {
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var peerSawCancellation = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var graph = new WorkGraphBuilder<WorkContext>()
            .Add("fail", new DelegateStep((_, _) =>
            {
                started.TrySetResult(true);
                throw new InvalidOperationException("boom");
            }))
            .Add("peer", new DelegateStep(async (_, ct) =>
            {
                await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, ct);
                }
                catch (OperationCanceledException)
                {
                    peerSawCancellation.TrySetResult(true);
                    throw;
                }
            }))
            .Build();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            graph.RunAsync(new WorkContext(), CancellationToken.None).AsTask());

        Assert.Equal("boom", ex.Message);
        var cancelled = await peerSawCancellation.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(cancelled);
    }

    [Fact]
    public async Task RunAsync_PropagatesCallerCancellation()
    {
        using var cts = new CancellationTokenSource();
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var graph = new WorkGraphBuilder<WorkContext>()
            .Add("slow", new DelegateStep(async (_, ct) =>
            {
                started.TrySetResult(true);
                await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            }))
            .Build();

        var run = graph.RunAsync(new WorkContext(), cts.Token).AsTask();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
    }

    [Fact]
    public void Add_WhenNullStep_ThrowsArgumentNullException()
    {
        var builder = new WorkGraphBuilder<WorkContext>();
        Assert.Throws<ArgumentNullException>(() => builder.Add("A", null!));
    }

    [Fact]
    public void Add_WhenNullOrWhitespaceId_ThrowsArgumentException()
    {
        var builder = new WorkGraphBuilder<WorkContext>();
        Assert.Throws<ArgumentException>(() => builder.Add(" ", new NoOpStep()));
        Assert.Throws<ArgumentException>(() => builder.Add(null!, new NoOpStep()));
    }

    [Fact]
    public void WorkGraphAttribute_StoresContextType()
    {
        var attribute = new WorkGraphAttribute(typeof(WorkContext));
        Assert.Equal(typeof(WorkContext), attribute.ContextType);
    }

    [Fact]
    public void WorkStepAttribute_StoresGraphIdAndDependsOn()
    {
        var attribute = new WorkStepAttribute(typeof(WorkGraphTests))
        {
            Id = "auth",
            DependsOn = new[] { "config" },
        };

        Assert.Equal(typeof(WorkGraphTests), attribute.Graph);
        Assert.Equal("auth", attribute.Id);
        Assert.Equal(new[] { "config" }, attribute.DependsOn);
    }

    private sealed class WorkContext;

    private sealed class NoOpStep : IWorkStep<WorkContext>
    {
        public ValueTask ExecuteAsync(WorkContext context, CancellationToken cancellationToken) => default;
    }

    private sealed class RecordingStep : IWorkStep<WorkContext>
    {
        private readonly string _id;
        private readonly List<string> _log;
        private readonly Func<Task>? _onExecute;

        public RecordingStep(string id, List<string> log, Func<Task>? onExecute = null)
        {
            _id = id;
            _log = log;
            _onExecute = onExecute;
        }

        public async ValueTask ExecuteAsync(WorkContext context, CancellationToken cancellationToken)
        {
            if (_onExecute is not null)
            {
                await _onExecute().ConfigureAwait(false);
            }

            lock (_log)
            {
                _log.Add(_id);
            }
        }
    }

    private sealed class DelegateStep : IWorkStep<WorkContext>
    {
        private readonly Func<WorkContext, CancellationToken, ValueTask> _execute;

        public DelegateStep(Func<WorkContext, CancellationToken, ValueTask> execute) =>
            _execute = execute;

        public ValueTask ExecuteAsync(WorkContext context, CancellationToken cancellationToken) =>
            _execute(context, cancellationToken);
    }
}
