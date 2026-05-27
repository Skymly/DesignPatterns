using DesignPatterns.Behavioral;

namespace DesignPatterns.Tests.Integration.Strategy;

public interface IIntegrationPaymentStrategy
{
    string Pay(decimal amount);
}

[RegisterStrategy<IIntegrationPaymentStrategy>("alpha")]
public sealed class AlphaPayment : IIntegrationPaymentStrategy
{
    public string Pay(decimal amount) => $"Alpha:{amount}";
}

[RegisterStrategy<IIntegrationPaymentStrategy>("beta")]
public sealed class BetaPayment : IIntegrationPaymentStrategy
{
    public string Pay(decimal amount) => $"Beta:{amount}";
}

public sealed class StrategyRegistryIntegrationTests
{
    [Fact]
    public void GeneratedRegistry_ResolvesRegisteredStrategies()
    {
        var registry = IntegrationPaymentStrategyRegistry.Instance;

        Assert.True(registry.TryGet(IntegrationPaymentStrategyKeys.Alpha, out var alpha));
        Assert.True(registry.TryGet(IntegrationPaymentStrategyKeys.Beta, out var beta));

        Assert.Equal("Alpha:10", alpha!.Pay(10m));
        Assert.Equal("Beta:20", beta!.Pay(20m));
    }

    [Fact]
    public void GeneratedRegistry_ReturnsFalseForUnknownKey()
    {
        var registry = IntegrationPaymentStrategyRegistry.Instance;

        Assert.False(registry.TryGet("missing", out _));
    }

    [Fact]
    public void GeneratedRegistry_GetThrowsForUnknownKey()
    {
        var registry = IntegrationPaymentStrategyRegistry.Instance;

        var ex = Assert.Throws<StrategyNotFoundException>(() => registry.Get("missing"));
        Assert.NotNull(ex);
    }
}
