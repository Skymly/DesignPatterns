using DesignPatterns.Behavioral;

namespace DesignPatterns.Tests.Behavioral;

public sealed class StrategyMarkerInterfaceTests
{
    private sealed class DoubleStrategy : IStrategy<int, int>
    {
        public int Execute(int input) => input * 2;
    }

    private sealed class AsyncLengthStrategy : IAsyncStrategy<string, int>
    {
        public ValueTask<int> ExecuteAsync(string input, CancellationToken cancellationToken = default) =>
            new ValueTask<int>(input.Length);
    }

    [Fact]
    public void IStrategy_Execute_ReturnsExpectedResult()
    {
        IStrategy<int, int> strategy = new DoubleStrategy();
        Assert.Equal(10, strategy.Execute(5));
    }

    [Fact]
    public async Task IAsyncStrategy_ExecuteAsync_ReturnsExpectedResult()
    {
        IAsyncStrategy<string, int> strategy = new AsyncLengthStrategy();
        var result = await strategy.ExecuteAsync("hello");
        Assert.Equal(5, result);
    }
}
