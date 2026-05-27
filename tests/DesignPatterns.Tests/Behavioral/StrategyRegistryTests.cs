using DesignPatterns.Behavioral;

namespace DesignPatterns.Tests.Behavioral;

public sealed class StrategyRegistryTests
{
    private interface ITestStrategy
    {
        string Name { get; }
    }

    private sealed class AlphaStrategy : ITestStrategy
    {
        public string Name => "alpha";
    }

    private sealed class BetaStrategy : ITestStrategy
    {
        public string Name => "beta";
    }

    [Fact]
    public void TryGet_ReturnsRegisteredStrategy()
    {
        var registry = new StrategyRegistryBuilder<string, ITestStrategy>()
            .Register("alpha", new AlphaStrategy())
            .Register("beta", new BetaStrategy())
            .Build();

        Assert.True(registry.TryGet("alpha", out var strategy));
        Assert.Equal("alpha", strategy.Name);
    }

    [Fact]
    public void Get_ThrowsWhenKeyMissing()
    {
        var registry = new StrategyRegistryBuilder<string, ITestStrategy>()
            .Register("alpha", new AlphaStrategy())
            .Build();

        var ex = Assert.Throws<StrategyNotFoundException>(() => registry.Get("missing"));
        Assert.Contains("missing", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Register_DuplicateKey_Throws()
    {
        var builder = new StrategyRegistryBuilder<string, ITestStrategy>()
            .Register("alpha", new AlphaStrategy());

        Assert.Throws<ArgumentException>(() => builder.Register("alpha", new BetaStrategy()));
    }

    [Fact]
    public void Keys_ReturnsAllRegisteredKeys()
    {
        var registry = new StrategyRegistryBuilder<string, ITestStrategy>()
            .Register("alpha", new AlphaStrategy())
            .Register("beta", new BetaStrategy())
            .Build();

        Assert.Equal(2, registry.Keys.Count);
        Assert.Contains("alpha", registry.Keys);
        Assert.Contains("beta", registry.Keys);
    }

    [Fact]
    public void Get_ReturnsSameInstanceAsRegistered()
    {
        var alpha = new AlphaStrategy();
        var registry = new StrategyRegistryBuilder<string, ITestStrategy>()
            .Register("alpha", alpha)
            .Build();

        Assert.Same(alpha, registry.Get("alpha"));
    }

    [Fact]
    public void TryGet_ReturnsFalseForMissingKey()
    {
        var registry = new StrategyRegistryBuilder<string, ITestStrategy>()
            .Register("alpha", new AlphaStrategy())
            .Build();

        Assert.False(registry.TryGet("missing", out _));
    }

    [Fact]
    public void Register_WithFactory_InvokesOnceAtBuildTime()
    {
        var callCount = 0;
        var registry = new StrategyRegistryBuilder<string, ITestStrategy>()
            .Register("alpha", () =>
            {
                callCount++;
                return new AlphaStrategy();
            })
            .Build();

        Assert.Equal(1, callCount);
        Assert.Equal("alpha", registry.Get("alpha").Name);
    }

    [Fact]
    public void Register_NullKey_Throws()
    {
        var builder = new StrategyRegistryBuilder<string, ITestStrategy>();

        Assert.Throws<ArgumentNullException>(() => builder.Register(null!, new AlphaStrategy()));
    }

    [Fact]
    public void Register_NullStrategy_Throws()
    {
        var builder = new StrategyRegistryBuilder<string, ITestStrategy>();

        Assert.Throws<ArgumentNullException>(() => builder.Register("alpha", (ITestStrategy)null!));
    }

    [Fact]
    public void StrategyRegistry_NullDictionary_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new StrategyRegistry<string, ITestStrategy>(null!));
    }
}
