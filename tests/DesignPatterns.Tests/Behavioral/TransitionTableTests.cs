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

    [Fact]
    public void TryTransition_WithGuardReturningTrue_FiresTransition()
    {
        var table = new TransitionTableBuilder<OrderStatus, OrderTrigger>()
            .WithInitial(OrderStatus.Draft)
            .Add(OrderStatus.Draft, OrderTrigger.Submit, OrderStatus.Submitted,
                guard: static (_, _) => true)
            .Build();

        Assert.True(table.TryTransition(OrderStatus.Draft, OrderTrigger.Submit, out var next));
        Assert.Equal(OrderStatus.Submitted, next);
    }

    [Fact]
    public void TryTransition_WithGuardReturningFalse_BlocksTransition()
    {
        var table = new TransitionTableBuilder<OrderStatus, OrderTrigger>()
            .WithInitial(OrderStatus.Draft)
            .Add(OrderStatus.Draft, OrderTrigger.Submit, OrderStatus.Submitted,
                guard: static (_, _) => false)
            .Build();

        Assert.False(table.TryTransition(OrderStatus.Draft, OrderTrigger.Submit, out _));
    }

    [Fact]
    public void TryTransition_WithGuardReceivesStateAndTrigger()
    {
        OrderStatus? capturedFrom = null;
        OrderTrigger? capturedTrigger = null;

        var table = new TransitionTableBuilder<OrderStatus, OrderTrigger>()
            .WithInitial(OrderStatus.Draft)
            .Add(OrderStatus.Draft, OrderTrigger.Submit, OrderStatus.Submitted,
                guard: (from, trigger) =>
                {
                    capturedFrom = from;
                    capturedTrigger = trigger;
                    return true;
                })
            .Build();

        table.TryTransition(OrderStatus.Draft, OrderTrigger.Submit, out _);

        Assert.Equal(OrderStatus.Draft, capturedFrom);
        Assert.Equal(OrderTrigger.Submit, capturedTrigger);
    }

    [Fact]
    public void TryTransition_WithoutGuard_BehavesSameAsBefore()
    {
        var table = new TransitionTableBuilder<OrderStatus, OrderTrigger>()
            .WithInitial(OrderStatus.Draft)
            .Add(OrderStatus.Draft, OrderTrigger.Submit, OrderStatus.Submitted, guard: null)
            .Build();

        Assert.True(table.TryTransition(OrderStatus.Draft, OrderTrigger.Submit, out var next));
        Assert.Equal(OrderStatus.Submitted, next);
    }

    [Fact]
    public void GetAllowedTriggers_IncludesGuardedTransitions()
    {
        var table = new TransitionTableBuilder<OrderStatus, OrderTrigger>()
            .WithInitial(OrderStatus.Draft)
            .Add(OrderStatus.Draft, OrderTrigger.Submit, OrderStatus.Submitted,
                guard: static (_, _) => false)
            .Build();

        // Guarded transitions still appear in allowed triggers — the guard
        // only affects TryTransition, not the trigger listing.
        Assert.Single(table.GetAllowedTriggers(OrderStatus.Draft));
    }

    [Fact]
    public void CanTransitionFrom_ReturnsTrueForGuardedEdge()
    {
        var table = new TransitionTableBuilder<OrderStatus, OrderTrigger>()
            .WithInitial(OrderStatus.Draft)
            .Add(OrderStatus.Draft, OrderTrigger.Submit, OrderStatus.Submitted,
                guard: static (_, _) => false)
            .Build();

        Assert.True(table.CanTransitionFrom(OrderStatus.Draft));
    }

    [Fact]
    public void TryTransition_WhenGuardThrows_PropagatesException()
    {
        var table = new TransitionTableBuilder<OrderStatus, OrderTrigger>()
            .WithInitial(OrderStatus.Draft)
            .Add(OrderStatus.Draft, OrderTrigger.Submit, OrderStatus.Submitted,
                guard: static (_, _) => throw new InvalidOperationException("guard failed"))
            .Build();

        Assert.Throws<InvalidOperationException>(() =>
            table.TryTransition(OrderStatus.Draft, OrderTrigger.Submit, out _));
    }

    [Fact]
    public void TryTransition_MixedGuardedAndUnguardedEdges_CoexistCorrectly()
    {
        var table = new TransitionTableBuilder<OrderStatus, OrderTrigger>()
            .WithInitial(OrderStatus.Draft)
            .Add(OrderStatus.Draft, OrderTrigger.Submit, OrderStatus.Submitted)
            .Add(OrderStatus.Draft, OrderTrigger.Cancel, OrderStatus.Cancelled,
                guard: static (_, _) => false)
            .Build();

        // Unguarded edge fires normally
        Assert.True(table.TryTransition(OrderStatus.Draft, OrderTrigger.Submit, out var submitted));
        Assert.Equal(OrderStatus.Submitted, submitted);

        // Guarded edge is blocked
        Assert.False(table.TryTransition(OrderStatus.Draft, OrderTrigger.Cancel, out _));

        // Both edges appear in allowed triggers
        Assert.Equal(new[] { OrderTrigger.Submit, OrderTrigger.Cancel },
            table.GetAllowedTriggers(OrderStatus.Draft));
    }

    // --- TryTransitionAsync with entry/exit actions ---

    [Fact]
    public async Task TryTransitionAsync_WithoutActions_BehavesLikeTryTransition()
    {
        var table = CreateOrderTable();

        var result = await table.TryTransitionAsync(OrderStatus.Draft, OrderTrigger.Submit, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(OrderStatus.Submitted, result.NextState);
    }

    [Fact]
    public async Task TryTransitionAsync_ReturnsFailedResultForMissingEdge()
    {
        var table = CreateOrderTable();

        var result = await table.TryTransitionAsync(OrderStatus.Paid, OrderTrigger.Pay, CancellationToken.None);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task TryTransitionAsync_WithSyncOnEnter_InvokesAction()
    {
        OrderStatus? capturedFrom = null;
        OrderStatus? capturedTo = null;
        OrderTrigger? capturedTrigger = null;

        var table = new TransitionTableBuilder<OrderStatus, OrderTrigger>()
            .WithInitial(OrderStatus.Draft)
            .Add(
                OrderStatus.Draft,
                OrderTrigger.Submit,
                OrderStatus.Submitted,
                guard: null,
                onEnterSync: (from, to, trigger) =>
                {
                    capturedFrom = from;
                    capturedTo = to;
                    capturedTrigger = trigger;
                },
                onExitSync: null)
            .Build();

        var result = await table.TryTransitionAsync(OrderStatus.Draft, OrderTrigger.Submit, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(OrderStatus.Draft, capturedFrom);
        Assert.Equal(OrderStatus.Submitted, capturedTo);
        Assert.Equal(OrderTrigger.Submit, capturedTrigger);
    }

    [Fact]
    public async Task TryTransitionAsync_WithSyncOnExit_InvokesActionBeforeOnEnter()
    {
        var callOrder = new List<string>();

        var table = new TransitionTableBuilder<OrderStatus, OrderTrigger>()
            .WithInitial(OrderStatus.Draft)
            .Add(
                OrderStatus.Draft,
                OrderTrigger.Submit,
                OrderStatus.Submitted,
                guard: null,
                onEnterSync: (_, _, _) => callOrder.Add("enter"),
                onExitSync: (_, _, _) => callOrder.Add("exit"))
            .Build();

        await table.TryTransitionAsync(OrderStatus.Draft, OrderTrigger.Submit, CancellationToken.None);

        // OnExit fires before OnEnter
        Assert.Equal(new[] { "exit", "enter" }, callOrder);
    }

    [Fact]
    public async Task TryTransitionAsync_WithAsyncOnEnter_AwaitsAction()
    {
        var tcs = new TaskCompletionSource<bool>();
        var actionInvoked = false;

        var table = new TransitionTableBuilder<OrderStatus, OrderTrigger>()
            .WithInitial(OrderStatus.Draft)
            .Add(
                OrderStatus.Draft,
                OrderTrigger.Submit,
                OrderStatus.Submitted,
                guard: null,
                onEnterAsync: async (_, _, _, ct) =>
                {
                    actionInvoked = true;
                    await tcs.Task.ConfigureAwait(false);
                },
                onExitAsync: null)
            .Build();

        // Complete the TCS after a short delay so the async action can proceed
        _ = Task.Delay(50).ContinueWith(_ => tcs.SetResult(true), TaskScheduler.Default);

        var result = await table.TryTransitionAsync(OrderStatus.Draft, OrderTrigger.Submit, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.True(actionInvoked);
    }

    [Fact]
    public async Task TryTransitionAsync_WithGuardReturningFalse_DoesNotInvokeActions()
    {
        var actionInvoked = false;

        var table = new TransitionTableBuilder<OrderStatus, OrderTrigger>()
            .WithInitial(OrderStatus.Draft)
            .Add(
                OrderStatus.Draft,
                OrderTrigger.Submit,
                OrderStatus.Submitted,
                guard: static (_, _) => false,
                onEnterSync: (_, _, _) => actionInvoked = true,
                onExitSync: null)
            .Build();

        var result = await table.TryTransitionAsync(OrderStatus.Draft, OrderTrigger.Submit, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.False(actionInvoked);
    }

    [Fact]
    public async Task TryTransitionAsync_RespectsCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var table = new TransitionTableBuilder<OrderStatus, OrderTrigger>()
            .WithInitial(OrderStatus.Draft)
            .Add(
                OrderStatus.Draft,
                OrderTrigger.Submit,
                OrderStatus.Submitted,
                guard: null,
                onEnterAsync: (_, _, _, ct) =>
                {
                    ct.ThrowIfCancellationRequested();
                    return default;
                },
                onExitAsync: null)
            .Build();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            table.TryTransitionAsync(OrderStatus.Draft, OrderTrigger.Submit, cts.Token).AsTask());
    }

    [Fact]
    public async Task TryTransitionAsync_WithMixedSyncAndAsyncActions_InvokesInOrder()
    {
        var callOrder = new List<string>();

        var table = new TransitionTableBuilder<OrderStatus, OrderTrigger>()
            .WithInitial(OrderStatus.Draft)
            .Add(
                OrderStatus.Draft,
                OrderTrigger.Submit,
                OrderStatus.Submitted,
                guard: null,
                onEnterSync: (_, _, _) => callOrder.Add("syncEnter"),
                onExitSync: (_, _, _) => callOrder.Add("syncExit"),
                onEnterAsync: (_, _, _, _) =>
                {
                    callOrder.Add("asyncEnter");
                    return default;
                },
                onExitAsync: (_, _, _, _) =>
                {
                    callOrder.Add("asyncExit");
                    return default;
                })
            .Build();

        await table.TryTransitionAsync(OrderStatus.Draft, OrderTrigger.Submit, CancellationToken.None);

        // Order: syncExit, asyncExit, syncEnter, asyncEnter
        Assert.Equal(new[] { "syncExit", "asyncExit", "syncEnter", "asyncEnter" }, callOrder);
    }

    [Fact]
    public async Task TryTransitionAsync_WithSyncOnExitOnly_InvokesAction()
    {
        var invoked = false;

        var table = new TransitionTableBuilder<OrderStatus, OrderTrigger>()
            .WithInitial(OrderStatus.Draft)
            .Add(
                OrderStatus.Draft,
                OrderTrigger.Submit,
                OrderStatus.Submitted,
                guard: null,
                onEnterSync: null,
                onExitSync: (_, _, _) => invoked = true)
            .Build();

        var result = await table.TryTransitionAsync(OrderStatus.Draft, OrderTrigger.Submit, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.True(invoked);
    }

    [Fact]
    public async Task TryTransitionAsync_WithAsyncOnExitOnly_AwaitsAction()
    {
        var invoked = false;

        var table = new TransitionTableBuilder<OrderStatus, OrderTrigger>()
            .WithInitial(OrderStatus.Draft)
            .Add(
                OrderStatus.Draft,
                OrderTrigger.Submit,
                OrderStatus.Submitted,
                guard: null,
                onEnterAsync: null,
                onExitAsync: (_, _, _, _) =>
                {
                    invoked = true;
                    return default;
                })
            .Build();

        var result = await table.TryTransitionAsync(OrderStatus.Draft, OrderTrigger.Submit, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.True(invoked);
    }

    [Fact]
    public async Task TryTransitionAsync_WhenGuardThrows_DoesNotInvokeActions()
    {
        var actionInvoked = false;

        var table = new TransitionTableBuilder<OrderStatus, OrderTrigger>()
            .WithInitial(OrderStatus.Draft)
            .Add(
                OrderStatus.Draft,
                OrderTrigger.Submit,
                OrderStatus.Submitted,
                guard: static (_, _) => throw new InvalidOperationException("guard failed"),
                onEnterSync: (_, _, _) => actionInvoked = true,
                onExitSync: null)
            .Build();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            table.TryTransitionAsync(OrderStatus.Draft, OrderTrigger.Submit, CancellationToken.None).AsTask());

        Assert.False(actionInvoked);
    }

    [Fact]
    public async Task TryTransitionAsync_WhenActionThrows_PropagatesException()
    {
        var table = new TransitionTableBuilder<OrderStatus, OrderTrigger>()
            .WithInitial(OrderStatus.Draft)
            .Add(
                OrderStatus.Draft,
                OrderTrigger.Submit,
                OrderStatus.Submitted,
                guard: null,
                onEnterSync: (_, _, _) => throw new InvalidOperationException("action failed"),
                onExitSync: null)
            .Build();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            table.TryTransitionAsync(OrderStatus.Draft, OrderTrigger.Submit, CancellationToken.None).AsTask());
    }
}
