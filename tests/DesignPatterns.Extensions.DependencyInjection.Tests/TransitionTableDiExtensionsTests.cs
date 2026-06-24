using DesignPatterns.Behavioral;
using DesignPatterns.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace DesignPatterns.Extensions.DependencyInjection.Tests;

public sealed class TransitionTableDiExtensionsTests
{
    private enum TestState { Idle, Active, Done }

    private enum TestTrigger { Start, Finish }

    [Fact]
    public void AddTransitionTable_RegistersTableAsSingleton()
    {
        var table = new TransitionTableBuilder<TestState, TestTrigger>()
            .WithInitial(TestState.Idle)
            .Add(TestState.Idle, TestTrigger.Start, TestState.Active)
            .Add(TestState.Active, TestTrigger.Finish, TestState.Done)
            .Build();

        var services = new ServiceCollection();
        services.AddTransitionTable(table);

        var provider = services.BuildServiceProvider();
        var resolved = provider.GetRequiredService<ITransitionTable<TestState, TestTrigger>>();

        Assert.Same(table, resolved);
    }

    [Fact]
    public void AddTransitionTable_ReturnsCollectionForChaining()
    {
        var table = new TransitionTableBuilder<TestState, TestTrigger>()
            .WithInitial(TestState.Idle)
            .Add(TestState.Idle, TestTrigger.Start, TestState.Active)
            .Build();

        var services = new ServiceCollection();
        var result = services.AddTransitionTable(table);

        Assert.Same(services, result);
    }

    [Fact]
    public void AddTransitionTable_NullServices_Throws()
    {
        var table = new TransitionTableBuilder<TestState, TestTrigger>()
            .WithInitial(TestState.Idle)
            .Build();

        Assert.Throws<ArgumentNullException>(() =>
            ((IServiceCollection)null!).AddTransitionTable(table));
    }

    [Fact]
    public void AddTransitionTable_NullTable_Throws()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentNullException>(() =>
            services.AddTransitionTable<TestState, TestTrigger>(null!));
    }

    [Fact]
    public void AddTransitionTable_TryAddDoesNotDuplicate()
    {
        var table1 = new TransitionTableBuilder<TestState, TestTrigger>()
            .WithInitial(TestState.Idle)
            .Add(TestState.Idle, TestTrigger.Start, TestState.Active)
            .Build();

        var table2 = new TransitionTableBuilder<TestState, TestTrigger>()
            .WithInitial(TestState.Active)
            .Build();

        var services = new ServiceCollection();
        services.AddTransitionTable(table1);
        services.AddTransitionTable(table2);

        var provider = services.BuildServiceProvider();
        var resolved = provider.GetRequiredService<ITransitionTable<TestState, TestTrigger>>();

        Assert.Same(table1, resolved);
        Assert.NotSame(table2, resolved);
    }

    [Fact]
    public void AddStateMachine_RegistersStateMachineResolvingTableFromContainer()
    {
        var table = new TransitionTableBuilder<TestState, TestTrigger>()
            .WithInitial(TestState.Idle)
            .Add(TestState.Idle, TestTrigger.Start, TestState.Active)
            .Build();

        var services = new ServiceCollection();
        services.AddTransitionTable(table);
        services.AddStateMachine<TestState, TestTrigger>();

        var provider = services.BuildServiceProvider();
        var machine = provider.GetRequiredService<IStateMachine<TestState, TestTrigger>>();

        Assert.Equal(TestState.Idle, machine.CurrentState);
        Assert.Same(table, machine.Table);

        Assert.True(machine.TryTransition(TestTrigger.Start, out _));
        Assert.Equal(TestState.Active, machine.CurrentState);
    }

    [Fact]
    public void AddStateMachine_AsTransient_CreatesNewInstanceEachTime()
    {
        var table = new TransitionTableBuilder<TestState, TestTrigger>()
            .WithInitial(TestState.Idle)
            .Add(TestState.Idle, TestTrigger.Start, TestState.Active)
            .Build();

        var services = new ServiceCollection();
        services.AddTransitionTable(table);
        services.AddStateMachine<TestState, TestTrigger>(ServiceLifetime.Transient);

        var provider = services.BuildServiceProvider();
        var machine1 = provider.GetRequiredService<IStateMachine<TestState, TestTrigger>>();
        var machine2 = provider.GetRequiredService<IStateMachine<TestState, TestTrigger>>();

        Assert.NotSame(machine1, machine2);
    }

    [Fact]
    public void AddStateMachine_NullServices_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ((IServiceCollection)null!).AddStateMachine<TestState, TestTrigger>());
    }

    [Fact]
    public void AddDecoratorStack_RegistersDecoratedService()
    {
        var services = new ServiceCollection();
        services.AddDecoratorStack<ICounterDiTest>(
            (sp, core) => new CounterDiTestDecorator(core),
            sp => new CounterDiTestCore(),
            ServiceLifetime.Singleton);

        var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<ICounterDiTest>();

        Assert.Equal(2, service.GetValue());
    }

    [Fact]
    public void AddDecoratorStack_NullServices_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ((IServiceCollection)null!).AddDecoratorStack<ICounterDiTest>(
                (_, core) => core,
                _ => new CounterDiTestCore()));
    }

    [Fact]
    public void AddDecoratorStack_NullBuildFunc_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ServiceCollection().AddDecoratorStack<ICounterDiTest>(
                null!,
                _ => new CounterDiTestCore()));
    }

    [Fact]
    public void AddDecoratorStack_NullCoreFactory_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ServiceCollection().AddDecoratorStack<ICounterDiTest>(
                (_, core) => core,
                null!));
    }

    private interface ICounterDiTest
    {
        int GetValue();
    }

    private sealed class CounterDiTestCore : ICounterDiTest
    {
        public int GetValue() => 1;
    }

    private sealed class CounterDiTestDecorator(ICounterDiTest inner) : ICounterDiTest
    {
        public int GetValue() => inner.GetValue() + 1;
    }
}
