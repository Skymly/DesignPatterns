using DesignPatterns.Behavioral;

namespace DesignPatterns.Tests.Behavioral;

public sealed class StrategyRegistryAsyncExtensionsTests
{
    private sealed class DoubleAsyncStrategy : IAsyncStrategy<int, int>
    {
        public CancellationToken LastToken { get; private set; }

        public ValueTask<int> ExecuteAsync(int input, CancellationToken cancellationToken = default)
        {
            LastToken = cancellationToken;
            return new ValueTask<int>(input * 2);
        }
    }

    private sealed class LengthAsyncStrategy : IAsyncStrategy<string, int>
    {
        public ValueTask<int> ExecuteAsync(string input, CancellationToken cancellationToken = default) =>
            new ValueTask<int>(input.Length);
    }

    private interface ILengthProcessor : IAsyncStrategy<string, int>
    {
    }

    private sealed class LengthProcessor : ILengthProcessor
    {
        public ValueTask<int> ExecuteAsync(string input, CancellationToken cancellationToken = default) =>
            new ValueTask<int>(input.Length);
    }

    [Fact]
    public async Task ExecuteAsync_WorksWithDerivedAsyncStrategyContract()
    {
        var registry = new StrategyRegistryBuilder<string, ILengthProcessor>()
            .Register("length", new LengthProcessor())
            .Build();

        var result = await registry.ExecuteAsync<ILengthProcessor, int, string>("length", "hello");

        Assert.Equal(5, result);
    }

    [Fact]
    public async Task TryExecuteAsync_WorksWithDerivedAsyncStrategyContract()
    {
        var registry = new StrategyRegistryBuilder<string, ILengthProcessor>()
            .Register("length", new LengthProcessor())
            .Build();

        var found = registry.TryExecuteAsync<ILengthProcessor, int, string>("length", "hello", out var result);

        Assert.True(found);
        Assert.Equal(5, await result);
    }

    [Fact]
    public async Task TryExecuteAsync_ReturnsFalseForMissingDerivedContractKey()
    {
        var registry = new StrategyRegistryBuilder<string, ILengthProcessor>()
            .Register("length", new LengthProcessor())
            .Build();

        var found = registry.TryExecuteAsync<ILengthProcessor, int, string>("missing", "hello", out var result);

        Assert.False(found);
        Assert.Equal(default(ValueTask<int>), result);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsExpectedResult()
    {
        var registry = new StrategyRegistryBuilder<string, IAsyncStrategy<int, int>>()
            .Register("double", new DoubleAsyncStrategy())
            .Build();

        var result = await registry.ExecuteAsync("double", 5);

        Assert.Equal(10, result);
    }

    [Fact]
    public void ExecuteAsync_ThrowsWhenKeyMissing()
    {
        var registry = new StrategyRegistryBuilder<string, IAsyncStrategy<int, int>>()
            .Register("double", new DoubleAsyncStrategy())
            .Build();

        var ex = Assert.Throws<StrategyNotFoundException>(() => registry.ExecuteAsync("missing", 5));

        Assert.Contains("missing", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ExecuteAsync_NullRegistry_Throws()
    {
        IStrategyRegistry<string, IAsyncStrategy<int, int>>? registry = null;

        Assert.Throws<ArgumentNullException>(() => registry!.ExecuteAsync("double", 5));
    }

    [Fact]
    public async Task ExecuteAsync_ForwardsCancellationToken()
    {
        var strategy = new DoubleAsyncStrategy();
        var registry = new StrategyRegistryBuilder<string, IAsyncStrategy<int, int>>()
            .Register("double", strategy)
            .Build();

        using var cts = new CancellationTokenSource();
        await registry.ExecuteAsync("double", 5, cts.Token);

        Assert.Equal(cts.Token, strategy.LastToken);
    }

    [Fact]
    public async Task TryExecuteAsync_ReturnsTrueAndResultWhenKeyExists()
    {
        var registry = new StrategyRegistryBuilder<string, IAsyncStrategy<string, int>>()
            .Register("length", new LengthAsyncStrategy())
            .Build();

        var found = registry.TryExecuteAsync("length", "hello", out var result);

        Assert.True(found);
        Assert.Equal(5, await result);
    }

    [Fact]
    public async Task TryExecuteAsync_ReturnsFalseWhenKeyMissing()
    {
        var registry = new StrategyRegistryBuilder<string, IAsyncStrategy<string, int>>()
            .Register("length", new LengthAsyncStrategy())
            .Build();

        var found = registry.TryExecuteAsync("missing", "hello", out var result);

        Assert.False(found);
        Assert.Equal(default(ValueTask<int>), result);
    }

    [Fact]
    public void TryExecuteAsync_NullRegistry_Throws()
    {
        IStrategyRegistry<string, IAsyncStrategy<string, int>>? registry = null;

        Assert.Throws<ArgumentNullException>(() =>
            registry!.TryExecuteAsync("length", "hello", out _));
    }

    // --- ExecuteTracedAsync tests ---

    [Fact]
    public async Task ExecuteTracedAsync_Success_ReturnsExecutedTrace()
    {
        var registry = new StrategyRegistryBuilder<string, IAsyncStrategy<int, int>>()
            .Register("double", new DoubleAsyncStrategy())
            .Build();

        var trace = await registry.ExecuteTracedAsync("double", 5);

        Assert.True(trace.Succeeded);
        Assert.Equal("double", trace.Key);
        Assert.Equal(StrategyExecutionStepStatus.Executed, trace.Status);
        Assert.Equal(10, trace.Output);
        Assert.Null(trace.Exception);
        Assert.True(trace.ElapsedMilliseconds >= 0);
    }

    [Fact]
    public async Task ExecuteTracedAsync_KeyNotFound_ReturnsKeyNotFoundTrace()
    {
        var registry = new StrategyRegistryBuilder<string, IAsyncStrategy<int, int>>()
            .Register("double", new DoubleAsyncStrategy())
            .Build();

        var trace = await registry.ExecuteTracedAsync("missing", 5);

        Assert.False(trace.Succeeded);
        Assert.Equal("missing", trace.Key);
        Assert.Equal(StrategyExecutionStepStatus.KeyNotFound, trace.Status);
        Assert.Null(trace.Exception);
    }

    [Fact]
    public async Task ExecuteTracedAsync_StrategyThrows_RecordsFailureAndRethrows()
    {
        var registry = new StrategyRegistryBuilder<string, IAsyncStrategy<int, int>>()
            .Register("fail", new ThrowingAsyncStrategy())
            .Build();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => registry.ExecuteTracedAsync("fail", 5).AsTask());

        Assert.Equal("strategy boom", ex.Message);
    }

    [Fact]
    public async Task ExecuteTracedAsync_StrategyThrows_ObserverNotified()
    {
        var registry = new StrategyRegistryBuilder<string, IAsyncStrategy<int, int>>()
            .Register("fail", new ThrowingAsyncStrategy())
            .Build();

        var observer = new CapturingExecutionObserver<int, int>();

        try
        {
            await registry.ExecuteTracedAsync("fail", 5, observer);
        }
        catch (InvalidOperationException)
        {
            // Expected.
        }

        Assert.NotNull(observer.LastFailedTrace);
        Assert.Equal("fail", observer.LastFailedTrace!.Key);
        Assert.Equal(StrategyExecutionStepStatus.Failed, observer.LastFailedTrace.Status);
        Assert.NotNull(observer.LastFailedTrace.Exception);
        Assert.Equal("strategy boom", observer.LastFailedTrace.Exception!.Message);
    }

    [Fact]
    public async Task ExecuteTracedAsync_Success_ObserverNotified()
    {
        var registry = new StrategyRegistryBuilder<string, IAsyncStrategy<int, int>>()
            .Register("double", new DoubleAsyncStrategy())
            .Build();

        var observer = new CapturingExecutionObserver<int, int>();

        var trace = await registry.ExecuteTracedAsync("double", 5, observer);

        Assert.True(trace.Succeeded);
        Assert.Equal(10, observer.LastCompletedOutput);
        Assert.Equal("double", observer.LastCompletedKey);
    }

    [Fact]
    public async Task ExecuteTracedAsync_NullObserver_WorksAsExpected()
    {
        var registry = new StrategyRegistryBuilder<string, IAsyncStrategy<int, int>>()
            .Register("double", new DoubleAsyncStrategy())
            .Build();

        var trace = await registry.ExecuteTracedAsync("double", 5, null);

        Assert.True(trace.Succeeded);
        Assert.Equal(10, trace.Output);
    }

    [Fact]
    public async Task ExecuteTracedAsync_DerivedContract_Works()
    {
        var registry = new StrategyRegistryBuilder<string, ILengthProcessor>()
            .Register("length", new LengthProcessor())
            .Build();

        var trace = await registry.ExecuteTracedAsync<ILengthProcessor, int, string>("length", "hello");

        Assert.True(trace.Succeeded);
        Assert.Equal(5, trace.Output);
    }

    [Fact]
    public async Task ExecuteTracedAsync_DerivedContract_KeyNotFound()
    {
        var registry = new StrategyRegistryBuilder<string, ILengthProcessor>()
            .Register("length", new LengthProcessor())
            .Build();

        var trace = await registry.ExecuteTracedAsync<ILengthProcessor, int, string>("missing", "hello");

        Assert.False(trace.Succeeded);
        Assert.Equal(StrategyExecutionStepStatus.KeyNotFound, trace.Status);
    }

    [Fact]
    public async Task ExecuteTracedAsync_GuardRejected_ReturnsGuardRejectedTrace()
    {
        var registry = new StrategyRegistryBuilder<string, IAsyncStrategy<int, int>>()
            .Register("double", new DoubleAsyncStrategy(), _ => false)
            .Build();

        var trace = await registry.ExecuteTracedAsync("double", 5);

        Assert.False(trace.Succeeded);
        Assert.Equal(StrategyExecutionStepStatus.GuardRejected, trace.Status);
    }

    [Fact]
    public async Task ExecuteTracedAsync_NullRegistry_Throws()
    {
        IStrategyRegistry<string, IAsyncStrategy<int, int>>? registry = null;

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            registry!.ExecuteTracedAsync("double", 5).AsTask());
    }

    // Helper types for traced tests

    private sealed class ThrowingAsyncStrategy : IAsyncStrategy<int, int>
    {
        public ValueTask<int> ExecuteAsync(int input, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("strategy boom");
    }

    private sealed class CapturingExecutionObserver<TInput, TOutput> : IStrategyExecutionObserver<TInput, TOutput>
    {
        public string? LastCompletedKey { get; private set; }
        public TOutput? LastCompletedOutput { get; private set; }
        public StrategyExecutionTrace<TOutput>? LastFailedTrace { get; private set; }

        public void OnExecutionCompleted(string key, TInput input, TOutput output, long elapsedMilliseconds)
        {
            LastCompletedKey = key;
            LastCompletedOutput = output;
        }

        public void OnExecutionFailed(string key, TInput input, StrategyExecutionTrace<TOutput> trace)
        {
            LastFailedTrace = trace;
        }
    }
}
