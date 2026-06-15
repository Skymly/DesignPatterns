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

    [Fact]
    public void InvalidTransitionException_ForTransition_IncludesStateAndTriggerInMessage()
    {
        var ex = InvalidTransitionException.ForTransition(OrderStatus.Draft, OrderTrigger.Pay);

        Assert.Equal("No transition registered for state 'Draft' and trigger 'Pay'.", ex.Message);
    }

    private enum OrderStatus
    {
        Draft,
    }

    private enum OrderTrigger
    {
        Pay,
    }
}
