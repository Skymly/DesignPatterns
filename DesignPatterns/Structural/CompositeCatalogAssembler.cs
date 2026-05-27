using System;
using System.Collections.Generic;
using System.Linq;

namespace DesignPatterns.Structural;

/// <summary>
/// Assembles composite trees from flat catalog entries.
/// </summary>
public static class CompositeCatalogAssembler
{
    /// <summary>
    /// Builds a single-root tree from catalog entries.
    /// </summary>
    /// <typeparam name="TNode">The composite contract type.</typeparam>
    /// <param name="entries">Catalog entries describing the tree.</param>
    /// <returns>The root node.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="entries"/> is null.</exception>
    /// <exception cref="CompositeAssemblyException">The catalog does not describe exactly one root.</exception>
    public static TNode Assemble<TNode>(IReadOnlyList<CompositeCatalogEntry<TNode>> entries)
        where TNode : class, ICompositeNode<TNode>
    {
        if (entries is null)
        {
            throw new ArgumentNullException(nameof(entries));
        }

        if (entries.Count == 0)
        {
            throw new CompositeAssemblyException("Composite catalog is empty.");
        }

        var entryByKey = entries.ToDictionary(entry => entry.Key, StringComparer.Ordinal);
        var childrenByParent = entries
            .Where(entry => entry.ParentKey is not null)
            .GroupBy(entry => entry.ParentKey!, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<CompositeCatalogEntry<TNode>>)group
                    .OrderBy(entry => entry.Order)
                    .ThenBy(entry => entry.Key, StringComparer.Ordinal)
                    .ToList(),
                StringComparer.Ordinal);

        var roots = entries
            .Where(entry => entry.ParentKey is null)
            .OrderBy(entry => entry.Order)
            .ThenBy(entry => entry.Key, StringComparer.Ordinal)
            .ToList();

        if (roots.Count != 1)
        {
            throw new CompositeAssemblyException(
                roots.Count == 0
                    ? "Composite catalog has no root entry (ParentKey must be null for exactly one part)."
                    : $"Composite catalog has {roots.Count} root entries; exactly one is required.");
        }

        var instances = new Dictionary<string, TNode>(StringComparer.Ordinal);
        AssembleSubtree(roots[0].Key, entryByKey, childrenByParent, instances);
        return instances[roots[0].Key];
    }

    private static void AssembleSubtree<TNode>(
        string key,
        IReadOnlyDictionary<string, CompositeCatalogEntry<TNode>> entryByKey,
        IReadOnlyDictionary<string, IReadOnlyList<CompositeCatalogEntry<TNode>>> childrenByParent,
        Dictionary<string, TNode> instances)
        where TNode : class, ICompositeNode<TNode>
    {
        if (instances.ContainsKey(key))
        {
            return;
        }

        if (!entryByKey.TryGetValue(key, out var entry))
        {
            throw new CompositeAssemblyException($"Composite catalog entry '{key}' was not found.");
        }

        var childEntries = childrenByParent.TryGetValue(key, out var children)
            ? children
            : Array.Empty<CompositeCatalogEntry<TNode>>();

        foreach (var childEntry in childEntries)
        {
            AssembleSubtree(childEntry.Key, entryByKey, childrenByParent, instances);
        }

        var childNodes = new List<TNode>(childEntries.Count);
        foreach (var childEntry in childEntries)
        {
            childNodes.Add(instances[childEntry.Key]);
        }

        var instance = CreateInstance<TNode>(entry.ImplementationType);
        SetChildren(instance, childNodes);
        instances[key] = instance;
    }

    private static TNode CreateInstance<TNode>(Type implementationType)
        where TNode : class, ICompositeNode<TNode>
    {
        if (!typeof(TNode).IsAssignableFrom(implementationType))
        {
            throw new CompositeAssemblyException(
                $"Type '{implementationType.FullName}' does not implement '{typeof(TNode).FullName}'.");
        }

        if (!typeof(ICompositeBuildable<TNode>).IsAssignableFrom(implementationType))
        {
            throw new CompositeAssemblyException(
                $"Type '{implementationType.FullName}' must implement ICompositeBuildable<{typeof(TNode).Name}>.");
        }

        try
        {
            return (TNode)Activator.CreateInstance(implementationType)!;
        }
        catch (Exception ex) when (ex is not CompositeAssemblyException)
        {
            throw new CompositeAssemblyException(
                $"Failed to create instance of '{implementationType.FullName}'. Ensure a public parameterless constructor exists.",
                ex);
        }
    }

    private static void SetChildren<TNode>(TNode instance, IReadOnlyList<TNode> children)
        where TNode : class, ICompositeNode<TNode>
    {
        if (instance is ICompositeBuildable<TNode> buildable)
        {
            buildable.SetChildren(children);
            return;
        }

        throw new CompositeAssemblyException(
            $"Type '{instance.GetType().FullName}' must implement ICompositeBuildable<{typeof(TNode).Name}>.");
    }
}
