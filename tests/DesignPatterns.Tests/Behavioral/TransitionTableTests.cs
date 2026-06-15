using DesignPatterns.Behavioral;

namespace DesignPatterns.Tests.Behavioral;

public sealed class TransitionTableTests
{
    private enum OrderStatus
    {
        Draft,
        Submitted,
        Paid,
        Cancelled,
    }

    private enum OrderTrigger
    {
        Submit,
        Pay,
        Cancel,
    }

    private static ITransitionTable<OrderStatus, OrderTrigger> CreateOrderTable() =>
        new TransitionTableBuilder<OrderStatus, OrderTrigger>()
            .WithInitial(OrderStatus.Draft)
            .Add(OrderStatus.Draft, OrderTrigger.Submit, OrderStatus.Submitted)
            .Add(OrderStatus.Submitted, OrderTrigger.Pay, OrderStatus.Paid)
            .Add(OrderStatus.Draft, OrderTrigger.Cancel, OrderStatus.Cancelled)
            .Add(OrderStatus.Submitted, OrderTrigger.Cancel, OrderStatus.Cancelled)
            .Build();

    [Fact]
    public void InitialState_ReturnsConfiguredValue()
    {
        var table = CreateOrderTable();

        Assert.Equal(OrderStatus.Draft, table.InitialState);
    }

    [Fact]
    public void TryTransition_ReturnsNextStateWhenEdgeExists()
    {
        var table = CreateOrderTable();

        Assert.True(table.TryTransition(OrderStatus.Draft, OrderTrigger.Submit, out var next));
        Assert.Equal(OrderStatus.Submitted, next);
    }

    [Fact]
    public void TryTransition_ReturnsFalseForMissingEdge()
    {
        var table = CreateOrderTable();

        Assert.False(table.TryTransition(OrderStatus.Paid, OrderTrigger.Pay, out _));
    }

    [Fact]
    public void Transition_ThrowsForMissingEdge()
    {
        var table = CreateOrderTable();

        var ex = Assert.Throws<InvalidTransitionException>(() =>
            table.Transition(OrderStatus.Paid, OrderTrigger.Pay));

        Assert.Contains("Paid", ex.Message, StringComparison.Ordinal);
        Assert.Contains("Pay", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Add_DuplicateEdge_Throws()
    {
        var builder = new TransitionTableBuilder<OrderStatus, OrderTrigger>()
            .WithInitial(OrderStatus.Draft)
            .Add(OrderStatus.Draft, OrderTrigger.Submit, OrderStatus.Submitted);

        Assert.Throws<ArgumentException>(() =>
            builder.Add(OrderStatus.Draft, OrderTrigger.Submit, OrderStatus.Cancelled));
    }

    [Fact]
    public void Add_SelfLoop_IsAllowed()
    {
        var table = new TransitionTableBuilder<OrderStatus, OrderTrigger>()
            .WithInitial(OrderStatus.Submitted)
            .Add(OrderStatus.Submitted, OrderTrigger.Submit, OrderStatus.Submitted)
            .Build();

        Assert.True(table.TryTransition(OrderStatus.Submitted, OrderTrigger.Submit, out var next));
        Assert.Equal(OrderStatus.Submitted, next);
    }

    [Fact]
    public void GetAllowedTriggers_ReturnsTriggersInDeclarationOrder()
    {
        var table = CreateOrderTable();

        var draftTriggers = table.GetAllowedTriggers(OrderStatus.Draft);

        Assert.Equal(new[] { OrderTrigger.Submit, OrderTrigger.Cancel }, draftTriggers);
    }

    [Fact]
    public void GetAllowedTriggers_ReturnsEmptyForTerminalState()
    {
        var table = CreateOrderTable();

        Assert.Empty(table.GetAllowedTriggers(OrderStatus.Paid));
    }

    [Fact]
    public void CanTransitionFrom_ReturnsTrueWhenOutgoingEdgesExist()
    {
        var table = CreateOrderTable();

        Assert.True(table.CanTransitionFrom(OrderStatus.Draft));
        Assert.False(table.CanTransitionFrom(OrderStatus.Paid));
    }

    [Fact]
    public void Build_WithoutInitial_Throws()
    {
        var builder = new TransitionTableBuilder<OrderStatus, OrderTrigger>()
            .Add(OrderStatus.Draft, OrderTrigger.Submit, OrderStatus.Submitted);

        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    [Fact]
    public void Transition_NullTable_Throws()
    {
        ITransitionTable<OrderStatus, OrderTrigger>? table = null;

        Assert.Throws<ArgumentNullException>(() => table!.Transition(OrderStatus.Draft, OrderTrigger.Submit));
    }
}
