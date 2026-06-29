using System.Collections.Generic;
using System.Linq;

namespace DesignPatterns.SourceGenerators.Generators.StateTransition;

/// <summary>
/// Composes hierarchical entry/exit action chains for flattened transitions.
/// <para>
/// In hierarchical mode, each flattened edge <c>(from, trigger) → to</c> may
/// cross hierarchy levels. The exit chain fires the exit action of every state
/// from <c>from</c> up to (but not including) the LCA of <c>from</c> and
/// <c>to</c>; the enter chain fires the enter action of every state from the
/// LCA down to (but not including the LCA) <c>to</c>. When a chain has more
/// than one action, a composite delegate is synthesized; otherwise the original
/// single reference (or <c>null</c>) is used. See RFC §8.
/// </para>
/// <para>
/// Per-state actions are collected from the original (pre-flatten) edges:
/// a state's exit action is the <c>OnExit</c> on any edge where
/// <c>from = state</c>; a state's enter action is the <c>OnEnter</c> on any
/// edge where <c>to = state</c>.
/// </para>
/// </summary>
internal static class ActionChainComposer
{
    /// <summary>
    /// Builds the per-state action map from the original (pre-flatten)
    /// transitions. For each state, the first <c>OnExit</c>/<c>OnEnter</c>
    /// encountered wins.
    /// </summary>
    public static StateActionMap BuildStateActionMap(List<ResolvedTransition> transitions)
    {
        var map = new StateActionMap();
        foreach (var t in transitions)
        {
            if (t.OnExitSyncReference is not null && !map.ExitSync.ContainsKey(t.FromMember))
            {
                map.ExitSync[t.FromMember] = t.OnExitSyncReference;
            }

            if (t.OnExitAsyncReference is not null && !map.ExitAsync.ContainsKey(t.FromMember))
            {
                map.ExitAsync[t.FromMember] = t.OnExitAsyncReference;
            }

            if (t.OnEnterSyncReference is not null && !map.EnterSync.ContainsKey(t.ToMember))
            {
                map.EnterSync[t.ToMember] = t.OnEnterSyncReference;
            }

            if (t.OnEnterAsyncReference is not null && !map.EnterAsync.ContainsKey(t.ToMember))
            {
                map.EnterAsync[t.ToMember] = t.OnEnterAsyncReference;
            }
        }

        return map;
    }

    /// <summary>
    /// Composes action chains for all transitions and produces override
    /// references + composite delegate definitions. Only called in
    /// hierarchical mode (parent map non-empty).
    /// </summary>
    public static ActionChainResult Compose(
        List<ResolvedTransition> transitions,
        Dictionary<string, string> parentMap,
        StateActionMap stateActions)
    {
        var overrides = new Dictionary<(string From, string Trigger), EdgeActionOverrides>();
        var delegateDefinitions = new List<CompositeDelegateDefinition>();

        foreach (var t in transitions)
        {
            var (exitStates, enterStates) = ComputeChains(t.FromMember, t.ToMember, parentMap);

            var exitSyncChain = CollectActions(exitStates, stateActions.ExitSync);
            var exitAsyncChain = CollectActions(exitStates, stateActions.ExitAsync);
            var enterSyncChain = CollectActions(enterStates, stateActions.EnterSync);
            var enterAsyncChain = CollectActions(enterStates, stateActions.EnterAsync);

            var onExitSync = ResolveChainReference(
                exitSyncChain, t.FromMember, t.TriggerMember,
                isExit: true, isAsync: false, delegateDefinitions);
            var onEnterSync = ResolveChainReference(
                enterSyncChain, t.FromMember, t.TriggerMember,
                isExit: false, isAsync: false, delegateDefinitions);
            var onExitAsync = ResolveChainReference(
                exitAsyncChain, t.FromMember, t.TriggerMember,
                isExit: true, isAsync: true, delegateDefinitions);
            var onEnterAsync = ResolveChainReference(
                enterAsyncChain, t.FromMember, t.TriggerMember,
                isExit: false, isAsync: true, delegateDefinitions);

            // Only record an override when something changed from the original.
            if (onExitSync != t.OnExitSyncReference
                || onEnterSync != t.OnEnterSyncReference
                || onExitAsync != t.OnExitAsyncReference
                || onEnterAsync != t.OnEnterAsyncReference)
            {
                overrides[(t.FromMember, t.TriggerMember)] = new EdgeActionOverrides(
                    onEnterSync, onExitSync, onEnterAsync, onExitAsync);
            }
        }

        return new ActionChainResult(overrides, delegateDefinitions);
    }

    /// <summary>
    /// Computes the exit and enter state chains for a transition
    /// <c>from → to</c> using the LCA algorithm (RFC §8.2/§8.3/§8.4).
    /// <list type="bullet">
    /// <item>Exit chain: states from <c>from</c> up to (not including) LCA.</item>
    /// <item>Enter chain: states from <c>to</c> up to (not including) LCA,
    /// reversed so the outermost state fires first.</item>
    /// </list>
    /// Self-loop (<c>from == to</c>) is a special case: exit = [from], enter = [to].
    /// </summary>
    public static (List<string> ExitStates, List<string> EnterStates) ComputeChains(
        string from,
        string to,
        Dictionary<string, string> parentMap)
    {
        // §8.4: self-loop — exit(from) → enter(from).
        if (string.Equals(from, to, System.StringComparison.Ordinal))
        {
            return (new List<string> { from }, new List<string> { to });
        }

        var lca = LowestCommonAncestor(from, to, parentMap);

        // Exit chain: from up to (not including) lca.
        var exitStates = new List<string>();
        var current = from;
        while (current is not null && !string.Equals(current, lca, System.StringComparison.Ordinal))
        {
            exitStates.Add(current);
            current = parentMap.TryGetValue(current, out var parent) ? parent : null;
        }

        // Enter chain: to up to (not including) lca, then reverse.
        var enterStates = new List<string>();
        current = to;
        while (current is not null && !string.Equals(current, lca, System.StringComparison.Ordinal))
        {
            enterStates.Add(current);
            current = parentMap.TryGetValue(current, out var parent) ? parent : null;
        }

        enterStates.Reverse();
        return (exitStates, enterStates);
    }

    /// <summary>
    /// Computes the lowest common ancestor of <paramref name="a"/> and
    /// <paramref name="b"/> using the parent map. Returns <c>null</c> when
    /// they share no common ancestor (root).
    /// </summary>
    public static string? LowestCommonAncestor(
        string a,
        string b,
        Dictionary<string, string> parentMap)
    {
        var ancestorsA = SelfAndAncestors(a, parentMap);
        var ancestorsB = SelfAndAncestors(b, parentMap);

        foreach (var node in ancestorsA)
        {
            if (ancestorsB.Contains(node))
            {
                return node;
            }
        }

        return null;
    }

    /// <summary>
    /// Returns the state itself followed by its ancestor chain (immediate
    /// parent first), using the parent map.
    /// </summary>
    private static List<string> SelfAndAncestors(string state, Dictionary<string, string> parentMap)
    {
        var result = new List<string> { state };
        var visited = new HashSet<string> { state };
        var current = state;
        while (parentMap.TryGetValue(current, out var parent))
        {
            if (!visited.Add(parent))
            {
                break; // Defensive — DP056 catches cycles at validation time.
            }

            result.Add(parent);
            current = parent;
        }

        return result;
    }

    /// <summary>
    /// Collects non-null action references for the given state chain from the
    /// provided per-state action dictionary.
    /// </summary>
    private static List<string> CollectActions(
        List<string> stateChain,
        Dictionary<string, string> actionMap)
    {
        var actions = new List<string>();
        foreach (var state in stateChain)
        {
            if (actionMap.TryGetValue(state, out var reference))
            {
                actions.Add(reference);
            }
        }

        return actions;
    }

    /// <summary>
    /// Resolves the effective action reference for an edge. When the chain has
    /// 0 actions, returns <c>null</c>. When the chain has 1 action, returns
    /// that action reference. When the chain has 2+ actions, creates a
    /// composite delegate definition and returns its name.
    /// </summary>
    private static string? ResolveChainReference(
        List<string> chain,
        string fromMember,
        string triggerMember,
        bool isExit,
        bool isAsync,
        List<CompositeDelegateDefinition> delegateDefinitions)
    {
        if (chain.Count == 0)
        {
            return null;
        }

        if (chain.Count == 1)
        {
            return chain[0];
        }

        // 2+ actions — synthesize a composite delegate.
        var prefix = isExit ? "CompositeExit" : "CompositeEnter";
        if (isAsync)
        {
            prefix += "Async";
        }

        var name = $"{prefix}_{fromMember}_{triggerMember}";
        delegateDefinitions.Add(new CompositeDelegateDefinition(
            name, isAsync, isExit, chain.ToList()));
        return name;
    }
}
