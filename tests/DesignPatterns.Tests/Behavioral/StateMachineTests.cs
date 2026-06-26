using DesignPatterns.Behavioral;

namespace DesignPatterns.Tests.Behavioral;

public sealed class StateMachineTests
{
    private enum LightState
    {
        Red,
        Green,
        Yellow,
    }

    private enum LightTrigger
    {
        Go,
        Yield,
        Reset,
    }

    private static ITransitionTable<LightState, LightTrigger> CreateLightTable() =>
        new TransitionTableBuilder<LightState, LightTrigger>()
            .WithInitial(LightState.Red)
            .Add(LightState.Red, LightTrigger.Go, LightState.Green)
            .Add(LightState.Green, LightTrigger.Yield, LightState.Yellow)
            .Add(LightState.Yellow, LightTrigger.Reset, LightState.Red)
            .Build();

    [Fact]
    public void Constructor_InitializesCurrentStateToInitialState()
    {
        var table = CreateLightTable();
        var machine = new StateMachine<LightState, LightTrigger>(table);

        Assert.Equal(LightState.Red, machine.CurrentState);
        Assert.Same(table, machine.Table);
    }

    [Fact]
    public void Constructor_NullTable_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new StateMachine<LightState, LightTrigger>(null!));
    }

    [Fact]
    public void TryTransition_UpdatesCurrentStateOnSuccess()
    {
        var machine = new StateMachine<LightState, LightTrigger>(CreateLightTable());

        Assert.True(machine.TryTransition(LightTrigger.Go, out var next));
        Assert.Equal(LightState.Green, next);
        Assert.Equal(LightState.Green, machine.CurrentState);

        Assert.True(machine.TryTransition(LightTrigger.Yield, out next));
        Assert.Equal(LightState.Yellow, next);
        Assert.Equal(LightState.Yellow, machine.CurrentState);
    }

    [Fact]
    public void TryTransition_ReturnsFalseAndKeepsStateOnInvalid()
    {
        var machine = new StateMachine<LightState, LightTrigger>(CreateLightTable());

        Assert.False(machine.TryTransition(LightTrigger.Yield, out _));
        Assert.Equal(LightState.Red, machine.CurrentState);
    }

    [Fact]
    public void Transition_ThrowsOnInvalid()
    {
        var machine = new StateMachine<LightState, LightTrigger>(CreateLightTable());

        Assert.Throws<InvalidTransitionException>(() =>
            machine.Transition(LightTrigger.Yield));
    }

    [Fact]
    public void Transition_ReturnsNewStateOnSuccess()
    {
        var machine = new StateMachine<LightState, LightTrigger>(CreateLightTable());

        var newState = machine.Transition(LightTrigger.Go);

        Assert.Equal(LightState.Green, newState);
        Assert.Equal(LightState.Green, machine.CurrentState);
    }

    [Fact]
    public async Task TryTransitionAsync_UpdatesCurrentStateOnSuccess()
    {
        var machine = new StateMachine<LightState, LightTrigger>(CreateLightTable());

        var result = await machine.TryTransitionAsync(LightTrigger.Go, CancellationToken.None);
        Assert.True(result.Succeeded);
        Assert.Equal(LightState.Green, machine.CurrentState);
    }

    [Fact]
    public async Task TryTransitionAsync_KeepsStateOnFailure()
    {
        var machine = new StateMachine<LightState, LightTrigger>(CreateLightTable());

        var result = await machine.TryTransitionAsync(LightTrigger.Reset, CancellationToken.None);
        Assert.False(result.Succeeded);
        Assert.Equal(LightState.Red, machine.CurrentState);
    }

    [Fact]
    public void CurrentState_CanBeSetInternally()
    {
        var machine = new StateMachine<LightState, LightTrigger>(CreateLightTable())
        {
            CurrentState = LightState.Yellow,
        };

        Assert.Equal(LightState.Yellow, machine.CurrentState);
        Assert.True(machine.TryTransition(LightTrigger.Reset, out _));
        Assert.Equal(LightState.Red, machine.CurrentState);
    }

    [Fact]
    public async Task TryTransitionAsync_WithActions_InvokesActionsBeforeStateUpdate()
    {
        var invoked = new List<string>();
        var table = new TransitionTableBuilder<LightState, LightTrigger>()
            .WithInitial(LightState.Red)
            .Add(
                LightState.Red,
                LightTrigger.Go,
                LightState.Green,
                guard: null,
                onEnterSync: (_, _, _) => invoked.Add("enter"),
                onExitSync: (_, _, _) => invoked.Add("exit"))
            .Build();
        var machine = new StateMachine<LightState, LightTrigger>(table);

        await machine.TryTransitionAsync(LightTrigger.Go, CancellationToken.None);

        Assert.Equal(new[] { "exit", "enter" }, invoked);
        Assert.Equal(LightState.Green, machine.CurrentState);
    }
}
