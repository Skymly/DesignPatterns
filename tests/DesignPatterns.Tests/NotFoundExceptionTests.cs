using DesignPatterns.Behavioral;
using DesignPatterns.Creational;

namespace DesignPatterns.Tests;

public sealed class NotFoundExceptionTests
{
    [Fact]
    public void StrategyNotFoundException_ForKey_IncludesKeyInMessage()
    {
        var ex = StrategyNotFoundException.ForKey("missing");

        Assert.Equal("No strategy registered for key 'missing'.", ex.Message);
    }

    [Fact]
    public void FactoryNotFoundException_ForKey_IncludesKeyInMessage()
    {
        var ex = FactoryNotFoundException.ForKey(42);

        Assert.Equal("No factory registered for key '42'.", ex.Message);
    }
}
