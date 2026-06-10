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
}
