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

    [Fact]
    public void GeneratedStateMachine_InitializesToInitialState()
    {
        var machine = new IntegrationOrderStatusStateMachine();

        Assert.Equal(IntegrationOrderStatus.Draft, machine.CurrentState);
        Assert.IsType<IntegrationOrderStatusTransitionTable>(machine.Table);
    }

    [Fact]
    public void GeneratedStateMachine_TryTransitionUpdatesCurrentState()
    {
        var machine = new IntegrationOrderStatusStateMachine();

        Assert.True(machine.TryTransition(IntegrationOrderTrigger.Submit, out var next));
        Assert.Equal(IntegrationOrderStatus.Submitted, next);
        Assert.Equal(IntegrationOrderStatus.Submitted, machine.CurrentState);

        Assert.True(machine.TryTransition(IntegrationOrderTrigger.Pay, out next));
        Assert.Equal(IntegrationOrderStatus.Paid, next);
        Assert.Equal(IntegrationOrderStatus.Paid, machine.CurrentState);
    }

    [Fact]
    public void GeneratedStateMachine_TryTransitionReturnsFalseOnInvalid()
    {
        var machine = new IntegrationOrderStatusStateMachine();

        Assert.False(machine.TryTransition(IntegrationOrderTrigger.Pay, out _));
        Assert.Equal(IntegrationOrderStatus.Draft, machine.CurrentState);
    }

    [Fact]
    public void GeneratedStateMachine_TransitionThrowsOnInvalid()
    {
        var machine = new IntegrationOrderStatusStateMachine();

        Assert.Throws<InvalidTransitionException>(() =>
            machine.Transition(IntegrationOrderTrigger.Pay));
    }

    [Fact]
    public async Task GeneratedStateMachine_TryTransitionAsyncUpdatesCurrentState()
    {
        var machine = new IntegrationOrderStatusStateMachine();

        var result = await machine.TryTransitionAsync(IntegrationOrderTrigger.Submit, CancellationToken.None);
        Assert.True(result.Succeeded);
        Assert.Equal(IntegrationOrderStatus.Submitted, machine.CurrentState);

        result = await machine.TryTransitionAsync(IntegrationOrderTrigger.Pay, CancellationToken.None);
        Assert.True(result.Succeeded);
        Assert.Equal(IntegrationOrderStatus.Paid, machine.CurrentState);
    }

    [Fact]
    public async Task GeneratedStateMachine_TryTransitionAsyncReturnsFalseOnInvalid()
    {
        var machine = new IntegrationOrderStatusStateMachine();

        var result = await machine.TryTransitionAsync(IntegrationOrderTrigger.Pay, CancellationToken.None);
        Assert.False(result.Succeeded);
        Assert.Equal(IntegrationOrderStatus.Draft, machine.CurrentState);
    }

    [Fact]
    public void GeneratedStateMachine_CurrentStateCanBeSetManually()
    {
        var machine = new IntegrationOrderStatusStateMachine
        {
            CurrentState = IntegrationOrderStatus.Submitted,
        };

        Assert.Equal(IntegrationOrderStatus.Submitted, machine.CurrentState);
        Assert.True(machine.TryTransition(IntegrationOrderTrigger.Pay, out _));
        Assert.Equal(IntegrationOrderStatus.Paid, machine.CurrentState);
    }
}
