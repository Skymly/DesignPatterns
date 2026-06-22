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
}
