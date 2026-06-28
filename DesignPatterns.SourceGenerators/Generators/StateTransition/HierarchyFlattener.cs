using System.Collections.Generic;
using System.Linq;

namespace DesignPatterns.SourceGenerators.Generators.StateTransition;

/// <summary>
/// Flattens hierarchical state machine transitions by inheriting parent-level
/// edges down to child states. A child state inherits all transitions declared
/// on its parent unless the child has its own transition for the same trigger
/// (child-priority override).
/// </summary>
internal static class HierarchyFlattener
{
    /// <summary>
    /// Flattens transitions by inheriting parent-level edges to children.
    /// </summary>
    /// <param name="transitions">Validated direct transitions.</param>
    /// <param name="parentMap">Child member name → parent member name.</param>
    /// <returns>Flattened transition list including inherited edges.</returns>
    public static List<ResolvedTransition> Flatten(
        List<ResolvedTransition> transitions,
        Dictionary<string, string> parentMap)
    {
        if (parentMap.Count == 0)
        {
            return transitions;
        }

        // Index existing transitions by (from, trigger) for quick lookup.
        var existingEdges = new HashSet<(string From, string Trigger)>();
        foreach (var t in transitions)
        {
            existingEdges.Add((t.FromMember, t.TriggerMember));
        }

        // Build child→ancestors map (ordered from immediate parent to root).
        var ancestorsMap = new Dictionary<string, List<string>>();
        foreach (var child in parentMap.Keys)
        {
            ancestorsMap[child] = GetAncestors(child, parentMap);
        }

        // For each child state, inherit transitions from ancestors that the
        // child doesn't already have (by trigger).
        var inherited = new List<ResolvedTransition>();
        foreach (var t in transitions)
        {
            // If this transition's source is a parent of some children, propagate it.
            // Find all children (direct or transitive) that have this state as an ancestor.
            foreach (var kvp in ancestorsMap)
            {
                var child = kvp.Key;
                var ancestors = kvp.Value;

                if (!ancestors.Contains(t.FromMember))
                {
                    continue;
                }

                var key = (child, t.TriggerMember);
                if (existingEdges.Contains(key))
                {
                    // Child already has its own transition for this trigger — skip.
                    continue;
                }

                // Also check if we already added an inherited one (from a closer ancestor).
                if (inherited.Any(i => i.FromMember == child && i.TriggerMember == t.TriggerMember))
                {
                    continue;
                }

                // Format the child's enum expression by replacing the parent member name.
                var childExpression = t.FromExpression.Replace(t.FromMember, child);
                inherited.Add(t.WithFrom(child, childExpression));
                existingEdges.Add(key);
            }
        }

        var result = new List<ResolvedTransition>(transitions);
        result.AddRange(inherited);
        return result;
    }

    /// <summary>
    /// Returns the ancestor chain for <paramref name="state"/>, ordered from
    /// immediate parent to root. Uses iterative traversal with cycle protection.
    /// </summary>
    public static List<string> GetAncestors(string state, Dictionary<string, string> parentMap)
    {
        var ancestors = new List<string>();
        var visited = new HashSet<string>();
        var current = state;
        while (parentMap.TryGetValue(current, out var parent))
        {
            if (!visited.Add(current))
            {
                break; // Cycle — defensive; DP056 catches this at validation time.
            }

            ancestors.Add(parent);
            current = parent;
        }

        return ancestors;
    }
}
