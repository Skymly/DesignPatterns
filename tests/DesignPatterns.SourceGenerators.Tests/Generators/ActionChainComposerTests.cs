using DesignPatterns.SourceGenerators.Generators.StateTransition;
using Xunit;

namespace DesignPatterns.SourceGenerators.Tests.Generators;

public sealed class ActionChainComposerTests
{
    // --- LCA (Lowest Common Ancestor) ---

    [Fact]
    public void LowestCommonAncestor_ReturnsSelf_WhenSameState()
    {
        var parentMap = new Dictionary<string, string>
        {
            ["Submitted"] = "Active",
        };

        var lca = ActionChainComposer.LowestCommonAncestor("Submitted", "Submitted", parentMap);

        Assert.Equal("Submitted", lca);
    }

    [Fact]
    public void LowestCommonAncestor_ReturnsParent_WhenSiblings()
    {
        var parentMap = new Dictionary<string, string>
        {
            ["Submitted"] = "Active",
            ["Paid"] = "Active",
        };

        var lca = ActionChainComposer.LowestCommonAncestor("Submitted", "Paid", parentMap);

        Assert.Equal("Active", lca);
    }

    [Fact]
    public void LowestCommonAncestor_ReturnsGrandparent_WhenDeepSiblings()
    {
        var parentMap = new Dictionary<string, string>
        {
            ["Processing"] = "Submitted",
            ["Submitted"] = "Active",
            ["Shipped"] = "Active",
        };

        var lca = ActionChainComposer.LowestCommonAncestor("Processing", "Shipped", parentMap);

        Assert.Equal("Active", lca);
    }

    [Fact]
    public void LowestCommonAncestor_ReturnsNull_WhenNoCommonAncestor()
    {
        var parentMap = new Dictionary<string, string>
        {
            ["Submitted"] = "Active",
        };

        var lca = ActionChainComposer.LowestCommonAncestor("Submitted", "Cancelled", parentMap);

        Assert.Null(lca);
    }

    [Fact]
    public void LowestCommonAncestor_ReturnsAncestor_WhenOneIsDescendantOfOther()
    {
        var parentMap = new Dictionary<string, string>
        {
            ["Processing"] = "Submitted",
            ["Submitted"] = "Active",
        };

        var lca = ActionChainComposer.LowestCommonAncestor("Processing", "Active", parentMap);

        Assert.Equal("Active", lca);
    }

    // --- ComputeChains (RFC §8.2/§8.3/§8.4) ---

    [Fact]
    public void ComputeChains_SelfLoop_ReturnsExitAndEnterOfSameState()
    {
        var parentMap = new Dictionary<string, string>
        {
            ["Submitted"] = "Active",
        };

        var (exitStates, enterStates) = ActionChainComposer.ComputeChains(
            "Submitted", "Submitted", parentMap);

        Assert.Equal(new[] { "Submitted" }, exitStates);
        Assert.Equal(new[] { "Submitted" }, enterStates);
    }

    [Fact]
    public void ComputeChains_NoCommonAncestor_ExitsAllFromAncestors_EntersAllToAncestors()
    {
        var parentMap = new Dictionary<string, string>
        {
            ["Submitted"] = "Active",
        };

        var (exitStates, enterStates) = ActionChainComposer.ComputeChains(
            "Submitted", "Cancelled", parentMap);

        // Exit: Submitted → Active (not including root/null)
        Assert.Equal(new[] { "Submitted", "Active" }, exitStates);
        // Enter: Cancelled (no ancestors)
        Assert.Equal(new[] { "Cancelled" }, enterStates);
    }

    [Fact]
    public void ComputeChains_LcaIsFrom_EnteringDescendant_OnlyEnterChain()
    {
        // Hierarchy: Active → Submitted → Processing
        var parentMap = new Dictionary<string, string>
        {
            ["Submitted"] = "Active",
            ["Processing"] = "Submitted",
        };

        // Transition: Active → Processing (entering a descendant)
        var (exitStates, enterStates) = ActionChainComposer.ComputeChains(
            "Active", "Processing", parentMap);

        // LCA = Active = from → exit chain is empty (don't exit Active)
        Assert.Empty(exitStates);
        // Enter: Processing → Submitted (reversed: Submitted, Processing), not including Active
        Assert.Equal(new[] { "Submitted", "Processing" }, enterStates);
    }

    [Fact]
    public void ComputeChains_LcaIsTo_ReturningToAncestor_OnlyExitChain()
    {
        // Hierarchy: Active → Submitted → Processing
        var parentMap = new Dictionary<string, string>
        {
            ["Submitted"] = "Active",
            ["Processing"] = "Submitted",
        };

        // Transition: Processing → Active (returning to ancestor)
        var (exitStates, enterStates) = ActionChainComposer.ComputeChains(
            "Processing", "Active", parentMap);

        // Exit: Processing → Submitted (not including Active = LCA = to)
        Assert.Equal(new[] { "Processing", "Submitted" }, exitStates);
        // Enter: empty (don't enter Active, we're already in its subtree)
        Assert.Empty(enterStates);
    }

    [Fact]
    public void ComputeChains_Siblings_ExitUpToLca_EnterDownFromLca()
    {
        var parentMap = new Dictionary<string, string>
        {
            ["Submitted"] = "Active",
            ["Paid"] = "Active",
        };

        // Transition: Submitted → Paid (siblings, LCA = Active)
        var (exitStates, enterStates) = ActionChainComposer.ComputeChains(
            "Submitted", "Paid", parentMap);

        // Exit: Submitted (not including Active = LCA)
        Assert.Equal(new[] { "Submitted" }, exitStates);
        // Enter: Paid (not including Active = LCA)
        Assert.Equal(new[] { "Paid" }, enterStates);
    }

    [Fact]
    public void ComputeChains_DeepHierarchy_ExitAndEnterChainsCorrect()
    {
        // Hierarchy: Root → A → B → C
        var parentMap = new Dictionary<string, string>
        {
            ["A"] = "Root",
            ["B"] = "A",
            ["C"] = "B",
        };

        // Transition: C → Root (deep descendant to root)
        var (exitStates, enterStates) = ActionChainComposer.ComputeChains(
            "C", "Root", parentMap);

        // LCA = Root = to → exit: C, B, A (not including Root)
        Assert.Equal(new[] { "C", "B", "A" }, exitStates);
        // Enter: empty (Root is the LCA = to)
        Assert.Empty(enterStates);
    }

    [Fact]
    public void ComputeChains_NoHierarchy_EmptyChains()
    {
        var parentMap = new Dictionary<string, string>();

        var (exitStates, enterStates) = ActionChainComposer.ComputeChains(
            "Draft", "Submitted", parentMap);

        // No hierarchy → LCA = null → exit: [Draft], enter: [Submitted]
        Assert.Equal(new[] { "Draft" }, exitStates);
        Assert.Equal(new[] { "Submitted" }, enterStates);
    }

    // --- BuildStateActionMap ---

    [Fact]
    public void BuildStateActionMap_CollectsExitActionsFromFromState()
    {
        var transitions = new List<ResolvedTransition>
        {
            new("Draft", "Submit", "Submitted", location: null!,
                "S.Draft", "T.Submit", "S.Submitted",
                guardMethodReference: null,
                onEnterSyncReference: null, onExitSyncReference: "Holder.OnExitDraft",
                onEnterAsyncReference: null, onExitAsyncReference: null),
            new("Submitted", "Pay", "Paid", location: null!,
                "S.Submitted", "T.Pay", "S.Paid",
                guardMethodReference: null,
                onEnterSyncReference: null, onExitSyncReference: "Holder.OnExitSubmitted",
                onEnterAsyncReference: null, onExitAsyncReference: null),
        };

        var map = ActionChainComposer.BuildStateActionMap(transitions);

        Assert.Equal("Holder.OnExitDraft", map.ExitSync["Draft"]);
        Assert.Equal("Holder.OnExitSubmitted", map.ExitSync["Submitted"]);
    }

    [Fact]
    public void BuildStateActionMap_CollectsEnterActionsFromToState()
    {
        var transitions = new List<ResolvedTransition>
        {
            new("Draft", "Submit", "Submitted", location: null!,
                "S.Draft", "T.Submit", "S.Submitted",
                guardMethodReference: null,
                onEnterSyncReference: "Holder.OnEnterSubmitted", onExitSyncReference: null,
                onEnterAsyncReference: null, onExitAsyncReference: null),
        };

        var map = ActionChainComposer.BuildStateActionMap(transitions);

        Assert.Equal("Holder.OnEnterSubmitted", map.EnterSync["Submitted"]);
    }

    [Fact]
    public void BuildStateActionMap_FirstWins_WhenMultipleExitActionsForSameState()
    {
        var transitions = new List<ResolvedTransition>
        {
            new("Draft", "Submit", "Submitted", location: null!,
                "S.Draft", "T.Submit", "S.Submitted",
                guardMethodReference: null,
                onEnterSyncReference: null, onExitSyncReference: "Holder.OnExitDraft1",
                onEnterAsyncReference: null, onExitAsyncReference: null),
            new("Draft", "Cancel", "Cancelled", location: null!,
                "S.Draft", "T.Cancel", "S.Cancelled",
                guardMethodReference: null,
                onEnterSyncReference: null, onExitSyncReference: "Holder.OnExitDraft2",
                onEnterAsyncReference: null, onExitAsyncReference: null),
        };

        var map = ActionChainComposer.BuildStateActionMap(transitions);

        // First one wins
        Assert.Equal("Holder.OnExitDraft1", map.ExitSync["Draft"]);
    }

    // --- Compose ---

    [Fact]
    public void Compose_CreatesCompositeDelegate_WhenChainHasMultipleActions()
    {
        var parentMap = new Dictionary<string, string>
        {
            ["Submitted"] = "Active",
            ["Paid"] = "Active",
        };

        var transitions = new List<ResolvedTransition>
        {
            new("Submitted", "Pay", "Paid", location: null!,
                "S.Submitted", "T.Pay", "S.Paid",
                guardMethodReference: null,
                onEnterSyncReference: null, onExitSyncReference: "Holder.OnExitSubmitted",
                onEnterAsyncReference: null, onExitAsyncReference: null),
            new("Active", "Cancel", "Cancelled", location: null!,
                "S.Active", "T.Cancel", "S.Cancelled",
                guardMethodReference: null,
                onEnterSyncReference: null, onExitSyncReference: "Holder.OnExitActive",
                onEnterAsyncReference: null, onExitAsyncReference: null),
            // Flattened inherited edge
            new("Submitted", "Cancel", "Cancelled", location: null!,
                "S.Submitted", "T.Cancel", "S.Cancelled",
                guardMethodReference: null,
                onEnterSyncReference: null, onExitSyncReference: "Holder.OnExitActive",
                onEnterAsyncReference: null, onExitAsyncReference: null),
        };

        var stateActionMap = ActionChainComposer.BuildStateActionMap(transitions);
        var result = ActionChainComposer.Compose(transitions, parentMap, stateActionMap);

        // The flattened edge (Submitted, Cancel) should have a composite exit delegate.
        // (Submitted, Pay, Paid) has LCA=Active so exit chain=[Submitted] → no composite.
        Assert.True(result.Overrides.TryGetValue(("Submitted", "Cancel"), out var overrides));
        Assert.Equal("CompositeExit_Submitted_Cancel", overrides.OnExitSyncReference);

        // The composite delegate definition should exist
        var composite = Assert.Single(result.DelegateDefinitions);
        Assert.Equal("CompositeExit_Submitted_Cancel", composite.Name);
        Assert.False(composite.IsAsync);
        Assert.True(composite.IsExit);
        Assert.Equal(2, composite.ActionReferences.Count);
        Assert.Equal("Holder.OnExitSubmitted", composite.ActionReferences[0]);
        Assert.Equal("Holder.OnExitActive", composite.ActionReferences[1]);
    }

    [Fact]
    public void Compose_NoOverride_WhenChainHasSingleAction()
    {
        var parentMap = new Dictionary<string, string>
        {
            ["Submitted"] = "Active",
        };

        var transitions = new List<ResolvedTransition>
        {
            new("Active", "Cancel", "Cancelled", location: null!,
                "S.Active", "T.Cancel", "S.Cancelled",
                guardMethodReference: null,
                onEnterSyncReference: null, onExitSyncReference: "Holder.OnExitActive",
                onEnterAsyncReference: null, onExitAsyncReference: null),
        };

        var stateActionMap = ActionChainComposer.BuildStateActionMap(transitions);
        var result = ActionChainComposer.Compose(transitions, parentMap, stateActionMap);

        // Single action → no override, no composite delegate
        Assert.Empty(result.Overrides);
        Assert.Empty(result.DelegateDefinitions);
    }

    [Fact]
    public void Compose_NoOverride_WhenChainIsEmpty()
    {
        var parentMap = new Dictionary<string, string>
        {
            ["Submitted"] = "Active",
        };

        var transitions = new List<ResolvedTransition>
        {
            new("Draft", "Submit", "Submitted", location: null!,
                "S.Draft", "T.Submit", "S.Submitted",
                guardMethodReference: null,
                onEnterSyncReference: null, onExitSyncReference: null,
                onEnterAsyncReference: null, onExitAsyncReference: null),
        };

        var stateActionMap = ActionChainComposer.BuildStateActionMap(transitions);
        var result = ActionChainComposer.Compose(transitions, parentMap, stateActionMap);

        Assert.Empty(result.Overrides);
        Assert.Empty(result.DelegateDefinitions);
    }
}
