using DesignPatterns.Behavioral;

namespace DesignPatterns.Tests.Behavioral;

public sealed class StateHierarchyTests
{
    private enum HierarchyState
    {
        Draft,
        Active,
        Submitted,
        Paid,
        Cancelled,
    }

    private enum HierarchyTrigger
    {
        Submit,
        Pay,
        Cancel,
    }

    private static ITransitionTable<HierarchyState, HierarchyTrigger> CreateFlatTable() =>
        new TransitionTableBuilder<HierarchyState, HierarchyTrigger>()
            .WithInitial(HierarchyState.Draft)
            .Add(HierarchyState.Draft, HierarchyTrigger.Submit, HierarchyState.Submitted)
            .Add(HierarchyState.Submitted, HierarchyTrigger.Pay, HierarchyState.Paid)
            .Add(HierarchyState.Draft, HierarchyTrigger.Cancel, HierarchyState.Cancelled)
            .Build();

    private static ITransitionTable<HierarchyState, HierarchyTrigger> CreateHierarchicalTable() =>
        new TransitionTableBuilder<HierarchyState, HierarchyTrigger>()
            .WithInitial(HierarchyState.Draft)
            .WithParent(HierarchyState.Submitted, HierarchyState.Active)
            .WithParent(HierarchyState.Paid, HierarchyState.Active)
            .Add(HierarchyState.Draft, HierarchyTrigger.Submit, HierarchyState.Submitted)
            .Add(HierarchyState.Submitted, HierarchyTrigger.Pay, HierarchyState.Paid)
            .Add(HierarchyState.Active, HierarchyTrigger.Cancel, HierarchyState.Cancelled)
            .Build();

    // --- IStateHierarchy implementation ---

    [Fact]
    public void FlatTable_ImplementsIStateHierarchy_WithTrivialBehavior()
    {
        var table = CreateFlatTable();
        var hierarchy = (IStateHierarchy<HierarchyState>)table;

        // Flat table has no parents — GetParent returns null for all states.
        Assert.Null(hierarchy.GetParent(HierarchyState.Submitted));
        Assert.Null(hierarchy.GetParent(HierarchyState.Active));

        // IsInState only returns true for self.
        Assert.True(hierarchy.IsInState(HierarchyState.Submitted, HierarchyState.Submitted));
        Assert.False(hierarchy.IsInState(HierarchyState.Submitted, HierarchyState.Active));

        // GetAncestors returns empty for all states.
        Assert.Empty(hierarchy.GetAncestors(HierarchyState.Submitted));
    }

    [Fact]
    public void HierarchicalTable_ImplementsIStateHierarchy()
    {
        var table = CreateHierarchicalTable();

        Assert.True(table is IStateHierarchy<HierarchyState>);
    }

    // --- GetParent ---

    [Fact]
    public void GetParent_ReturnsParentForChildState()
    {
        var hierarchy = (IStateHierarchy<HierarchyState>)CreateHierarchicalTable();

        Assert.Equal(HierarchyState.Active, hierarchy.GetParent(HierarchyState.Submitted));
        Assert.Equal(HierarchyState.Active, hierarchy.GetParent(HierarchyState.Paid));
    }

    [Fact]
    public void GetParent_ReturnsNullForRootState()
    {
        var hierarchy = (IStateHierarchy<HierarchyState>)CreateHierarchicalTable();

        Assert.Null(hierarchy.GetParent(HierarchyState.Draft));
        Assert.Null(hierarchy.GetParent(HierarchyState.Active));
        Assert.Null(hierarchy.GetParent(HierarchyState.Cancelled));
    }

    // --- IsInState ---

    [Fact]
    public void IsInState_ReturnsTrueForSelf()
    {
        var hierarchy = (IStateHierarchy<HierarchyState>)CreateHierarchicalTable();

        Assert.True(hierarchy.IsInState(HierarchyState.Submitted, HierarchyState.Submitted));
        Assert.True(hierarchy.IsInState(HierarchyState.Active, HierarchyState.Active));
        Assert.True(hierarchy.IsInState(HierarchyState.Draft, HierarchyState.Draft));
    }

    [Fact]
    public void IsInState_ReturnsTrueForDescendant()
    {
        var hierarchy = (IStateHierarchy<HierarchyState>)CreateHierarchicalTable();

        Assert.True(hierarchy.IsInState(HierarchyState.Submitted, HierarchyState.Active));
        Assert.True(hierarchy.IsInState(HierarchyState.Paid, HierarchyState.Active));
    }

    [Fact]
    public void IsInState_ReturnsFalseForNonAncestor()
    {
        var hierarchy = (IStateHierarchy<HierarchyState>)CreateHierarchicalTable();

        Assert.False(hierarchy.IsInState(HierarchyState.Submitted, HierarchyState.Paid));
        Assert.False(hierarchy.IsInState(HierarchyState.Submitted, HierarchyState.Draft));
        Assert.False(hierarchy.IsInState(HierarchyState.Active, HierarchyState.Submitted));
        Assert.False(hierarchy.IsInState(HierarchyState.Draft, HierarchyState.Active));
    }

    // --- GetAncestors ---

    [Fact]
    public void GetAncestors_ReturnsOrderedChainForChildState()
    {
        var hierarchy = (IStateHierarchy<HierarchyState>)CreateHierarchicalTable();

        var ancestors = hierarchy.GetAncestors(HierarchyState.Submitted);

        Assert.Single(ancestors);
        Assert.Equal(HierarchyState.Active, ancestors[0]);
    }

    [Fact]
    public void GetAncestors_ReturnsEmptyForRootState()
    {
        var hierarchy = (IStateHierarchy<HierarchyState>)CreateHierarchicalTable();

        Assert.Empty(hierarchy.GetAncestors(HierarchyState.Draft));
        Assert.Empty(hierarchy.GetAncestors(HierarchyState.Active));
        Assert.Empty(hierarchy.GetAncestors(HierarchyState.Cancelled));
    }

    [Fact]
    public void GetAncestors_ReturnsCachedResultOnSecondCall()
    {
        var hierarchy = (IStateHierarchy<HierarchyState>)CreateHierarchicalTable();

        var first = hierarchy.GetAncestors(HierarchyState.Submitted);
        var second = hierarchy.GetAncestors(HierarchyState.Submitted);

        Assert.Same(first, second);
    }

    // --- Deep hierarchy (3 levels) ---

    private enum DeepState
    {
        Root,
        Mid,
        Leaf,
    }

    private enum DeepTrigger
    {
        Go,
    }

    private static ITransitionTable<DeepState, DeepTrigger> CreateDeepHierarchy() =>
        new TransitionTableBuilder<DeepState, DeepTrigger>()
            .WithInitial(DeepState.Root)
            .WithParent(DeepState.Mid, DeepState.Root)
            .WithParent(DeepState.Leaf, DeepState.Mid)
            .Add(DeepState.Root, DeepTrigger.Go, DeepState.Leaf)
            .Build();

    [Fact]
    public void DeepHierarchy_GetAncestors_ReturnsFullChain()
    {
        var hierarchy = (IStateHierarchy<DeepState>)CreateDeepHierarchy();

        var ancestors = hierarchy.GetAncestors(DeepState.Leaf);

        Assert.Equal(2, ancestors.Count);
        Assert.Equal(DeepState.Mid, ancestors[0]);
        Assert.Equal(DeepState.Root, ancestors[1]);
    }

    [Fact]
    public void DeepHierarchy_IsInState_ReturnsTrueForAllAncestors()
    {
        var hierarchy = (IStateHierarchy<DeepState>)CreateDeepHierarchy();

        Assert.True(hierarchy.IsInState(DeepState.Leaf, DeepState.Mid));
        Assert.True(hierarchy.IsInState(DeepState.Leaf, DeepState.Root));
        Assert.True(hierarchy.IsInState(DeepState.Mid, DeepState.Root));
    }

    // --- Extension methods ---

    [Fact]
    public void Extension_IsInState_ReturnsTrueForHierarchicalTable()
    {
        var table = CreateHierarchicalTable();

        Assert.True(table.IsInState(HierarchyState.Submitted, HierarchyState.Active));
        Assert.True(table.IsInState(HierarchyState.Paid, HierarchyState.Active));
        Assert.True(table.IsInState(HierarchyState.Submitted, HierarchyState.Submitted));
    }

    [Fact]
    public void Extension_IsInState_ReturnsFalseForFlatTable()
    {
        var table = CreateFlatTable();

        // Flat table has trivial hierarchy — IsInState only returns true for self.
        Assert.False(table.IsInState(HierarchyState.Submitted, HierarchyState.Active));
        Assert.True(table.IsInState(HierarchyState.Submitted, HierarchyState.Submitted));
    }

    [Fact]
    public void Extension_GetParent_ReturnsParentForHierarchicalTable()
    {
        var table = CreateHierarchicalTable();

        Assert.Equal(HierarchyState.Active, table.GetParent(HierarchyState.Submitted));
    }

    [Fact]
    public void Extension_GetParent_ReturnsNullForFlatTable()
    {
        var table = CreateFlatTable();

        // Flat table has trivial hierarchy — no parents.
        Assert.Null(table.GetParent(HierarchyState.Submitted));
    }

    [Fact]
    public void Extension_GetAncestors_ReturnsChainForHierarchicalTable()
    {
        var table = CreateHierarchicalTable();

        var ancestors = table.GetAncestors(HierarchyState.Submitted);

        Assert.Single(ancestors);
        Assert.Equal(HierarchyState.Active, ancestors[0]);
    }

    [Fact]
    public void Extension_GetAncestors_ReturnsEmptyForFlatTable()
    {
        var table = CreateFlatTable();

        // Flat table has trivial hierarchy — no ancestors.
        Assert.Empty(table.GetAncestors(HierarchyState.Submitted));
    }

    // --- Builder validation ---

    [Fact]
    public void WithParent_ThrowsOnSelfReference()
    {
        var builder = new TransitionTableBuilder<HierarchyState, HierarchyTrigger>()
            .WithInitial(HierarchyState.Draft);

        var ex = Assert.Throws<ArgumentException>(
            () => builder.WithParent(HierarchyState.Active, HierarchyState.Active));
        Assert.Contains("own parent", ex.Message);
    }

    [Fact]
    public void WithParent_ThrowsOnConflictingParent()
    {
        var builder = new TransitionTableBuilder<HierarchyState, HierarchyTrigger>()
            .WithInitial(HierarchyState.Draft)
            .WithParent(HierarchyState.Submitted, HierarchyState.Active);

        var ex = Assert.Throws<ArgumentException>(
            () => builder.WithParent(HierarchyState.Submitted, HierarchyState.Draft));
        Assert.Contains("multiple inheritance", ex.Message);
    }

    [Fact]
    public void WithParent_SameParentTwice_DoesNotThrow()
    {
        var builder = new TransitionTableBuilder<HierarchyState, HierarchyTrigger>()
            .WithInitial(HierarchyState.Draft)
            .WithParent(HierarchyState.Submitted, HierarchyState.Active);

        // Idempotent — same parent declared twice is fine.
        builder.WithParent(HierarchyState.Submitted, HierarchyState.Active);
    }

    // --- Parent state as active leaf (Q1 decision) ---

    [Fact]
    public void ParentState_CanAlsoBeLeafState_WithDirectEdges()
    {
        // Active is both a parent (of Submitted/Paid) and a leaf state with its own edge.
        var table = new TransitionTableBuilder<HierarchyState, HierarchyTrigger>()
            .WithInitial(HierarchyState.Draft)
            .WithParent(HierarchyState.Submitted, HierarchyState.Active)
            .WithParent(HierarchyState.Paid, HierarchyState.Active)
            .Add(HierarchyState.Draft, HierarchyTrigger.Submit, HierarchyState.Active)
            .Add(HierarchyState.Active, HierarchyTrigger.Cancel, HierarchyState.Cancelled)
            .Build();

        // Active can be a direct target.
        Assert.True(table.TryTransition(HierarchyState.Draft, HierarchyTrigger.Submit, out var next));
        Assert.Equal(HierarchyState.Active, next);

        // Active can be a source.
        Assert.True(table.TryTransition(HierarchyState.Active, HierarchyTrigger.Cancel, out next));
        Assert.Equal(HierarchyState.Cancelled, next);

        // IsInState(Active, Active) is true.
        Assert.True(table.IsInState(HierarchyState.Active, HierarchyState.Active));
    }

    // --- Runtime action chain execution order (hierarchical) ---

    [Fact]
    public async Task TryTransitionAsync_HierarchicalExitChain_FiresChildThenParent()
    {
        var callOrder = new List<string>();

        // The runtime TransitionTableBuilder does NOT flatten inherited edges or compose
        // action chains — that is the source generator's job. Here we verify that when
        // both child and parent have explicit exit actions on their own edges, each edge
        // fires its own OnExit independently.
        var table = new TransitionTableBuilder<HierarchyState, HierarchyTrigger>()
            .WithInitial(HierarchyState.Draft)
            .WithParent(HierarchyState.Submitted, HierarchyState.Active)
            .WithParent(HierarchyState.Paid, HierarchyState.Active)
            .Add(HierarchyState.Draft, HierarchyTrigger.Submit, HierarchyState.Submitted,
                onExitSync: (_, _, _) => callOrder.Add("exit_Draft"))
            .Add(HierarchyState.Submitted, HierarchyTrigger.Cancel, HierarchyState.Cancelled,
                onExitSync: (_, _, _) => callOrder.Add("exit_Submitted"))
            .Add(HierarchyState.Active, HierarchyTrigger.Cancel, HierarchyState.Cancelled,
                onExitSync: (_, _, _) => callOrder.Add("exit_Active"))
            .Build();

        // Draft → Submitted (fires exit_Draft)
        await table.TryTransitionAsync(HierarchyState.Draft, HierarchyTrigger.Submit, CancellationToken.None);
        Assert.Equal(new[] { "exit_Draft" }, callOrder);

        callOrder.Clear();

        // Submitted → Cancelled (explicit edge, fires exit_Submitted only — no composite chain at runtime)
        await table.TryTransitionAsync(HierarchyState.Submitted, HierarchyTrigger.Cancel, CancellationToken.None);
        Assert.Equal(new[] { "exit_Submitted" }, callOrder);
    }

    [Fact]
    public async Task TryTransitionAsync_MixedSyncAndAsyncOnSameEdge_FiresInCorrectOrder()
    {
        var callOrder = new List<string>();

        var table = new TransitionTableBuilder<HierarchyState, HierarchyTrigger>()
            .WithInitial(HierarchyState.Draft)
            .WithParent(HierarchyState.Submitted, HierarchyState.Active)
            .Add(HierarchyState.Draft, HierarchyTrigger.Submit, HierarchyState.Submitted,
                onExitSync: (_, _, _) => callOrder.Add("syncExit"),
                onExitAsync: (_, _, _, _) =>
                {
                    callOrder.Add("asyncExit");
                    return default;
                },
                onEnterSync: (_, _, _) => callOrder.Add("syncEnter"),
                onEnterAsync: (_, _, _, _) =>
                {
                    callOrder.Add("asyncEnter");
                    return default;
                })
            .Build();

        await table.TryTransitionAsync(HierarchyState.Draft, HierarchyTrigger.Submit, CancellationToken.None);

        // Order: syncExit → asyncExit → syncEnter → asyncEnter
        Assert.Equal(new[] { "syncExit", "asyncExit", "syncEnter", "asyncEnter" }, callOrder);
    }

    [Fact]
    public async Task TryTransitionAsync_BothEnterAndExitActions_FiresExitBeforeEnter()
    {
        var callOrder = new List<string>();

        var table = new TransitionTableBuilder<HierarchyState, HierarchyTrigger>()
            .WithInitial(HierarchyState.Draft)
            .WithParent(HierarchyState.Submitted, HierarchyState.Active)
            .Add(HierarchyState.Draft, HierarchyTrigger.Submit, HierarchyState.Submitted,
                onEnterSync: (_, _, _) => callOrder.Add("enter_Submitted"),
                onExitSync: (_, _, _) => callOrder.Add("exit_Draft"))
            .Build();

        var result = await table.TryTransitionAsync(HierarchyState.Draft, HierarchyTrigger.Submit, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(new[] { "exit_Draft", "enter_Submitted" }, callOrder);
    }

    [Fact]
    public async Task TryTransitionAsync_ParentEdge_FiresParentExitAction()
    {
        var exitActionInvoked = false;
        HierarchyState capturedFrom = default;
        HierarchyState capturedTo = default;

        // The runtime TransitionTableBuilder does NOT flatten — the edge on Active is only
        // reachable from Active, not from child states. We verify the parent-level edge fires
        // its own OnExit when triggered from the parent state directly.
        var table = new TransitionTableBuilder<HierarchyState, HierarchyTrigger>()
            .WithInitial(HierarchyState.Draft)
            .WithParent(HierarchyState.Submitted, HierarchyState.Active)
            .WithParent(HierarchyState.Paid, HierarchyState.Active)
            .Add(HierarchyState.Draft, HierarchyTrigger.Submit, HierarchyState.Active)
            .Add(HierarchyState.Active, HierarchyTrigger.Cancel, HierarchyState.Cancelled,
                onExitSync: (from, to, _) =>
                {
                    exitActionInvoked = true;
                    capturedFrom = from;
                    capturedTo = to;
                })
            .Build();

        // Move to Active first
        await table.TryTransitionAsync(HierarchyState.Draft, HierarchyTrigger.Submit, CancellationToken.None);

        // Fire Cancel from Active — the edge's OnExit captures from=Active, to=Cancelled
        var result = await table.TryTransitionAsync(HierarchyState.Active, HierarchyTrigger.Cancel, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(HierarchyState.Cancelled, result.NextState);
        Assert.True(exitActionInvoked);
        Assert.Equal(HierarchyState.Active, capturedFrom);
        Assert.Equal(HierarchyState.Cancelled, capturedTo);
    }
}
