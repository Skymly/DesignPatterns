using DesignPatterns.Behavioral;

namespace DesignPatterns.Tests.Integration.StateMachine;

public enum IntegrationOrderStatus
{
    Draft,
    Submitted,
    Paid,
}

public enum IntegrationOrderTrigger
{
    Submit,
    Pay,
}

[StateMachine(typeof(IntegrationOrderStatus), typeof(IntegrationOrderTrigger), Initial = IntegrationOrderStatus.Draft)]
[Transition(IntegrationOrderStatus.Draft, IntegrationOrderTrigger.Submit, IntegrationOrderStatus.Submitted)]
[Transition(IntegrationOrderStatus.Submitted, IntegrationOrderTrigger.Pay, IntegrationOrderStatus.Paid)]
public static partial class IntegrationOrderMachine;

public sealed class StateTransitionIntegrationTests
{
    [Fact]
    public void GeneratedTable_ResolvesTransitions()
    {
        Assert.True(IntegrationOrderMachine.TryTransition(
            IntegrationOrderStatus.Draft,
            IntegrationOrderTrigger.Submit,
            out var submitted));
        Assert.Equal(IntegrationOrderStatus.Submitted, submitted);

        Assert.True(IntegrationOrderMachine.TryTransition(
            IntegrationOrderStatus.Submitted,
            IntegrationOrderTrigger.Pay,
            out var paid));
        Assert.Equal(IntegrationOrderStatus.Paid, paid);
    }

    [Fact]
    public void GeneratedTable_ReturnsFalseForInvalidTransition()
    {
        Assert.False(IntegrationOrderMachine.TryTransition(
            IntegrationOrderStatus.Paid,
            IntegrationOrderTrigger.Pay,
            out _));
    }

    [Fact]
    public void GeneratedTable_InstanceMatchesHolder()
    {
        Assert.Equal(
            IntegrationOrderStatusTransitionTable.Instance.InitialState,
            IntegrationOrderMachine.InitialState);
    }
}
