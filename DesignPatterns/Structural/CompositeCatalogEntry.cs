using System;

namespace DesignPatterns.Structural;

/// <summary>
/// Describes a composite part in a flat catalog used to assemble a tree at runtime.
/// </summary>
/// <typeparam name="TNode">The composite contract type.</typeparam>
public sealed class CompositeCatalogEntry<TNode>
    where TNode : ICompositeNode<TNode>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CompositeCatalogEntry{TNode}"/> class.
    /// </summary>
    public CompositeCatalogEntry(string key, string? parentKey, int order, Type implementationType)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Key cannot be null or whitespace.", nameof(key));
        }

        Key = key;
        ParentKey = parentKey;
        Order = order;
        ImplementationType = implementationType ?? throw new ArgumentNullException(nameof(implementationType));
    }

    /// <summary>
    /// Unique key for this part within the catalog.
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// Parent key, or <see langword="null"/> for a root candidate.
    /// </summary>
    public string? ParentKey { get; }

    /// <summary>
    /// Order among siblings sharing the same parent. Lower values come first.
    /// </summary>
    public int Order { get; }

    /// <summary>
    /// Concrete implementation type to instantiate.
    /// </summary>
    public Type ImplementationType { get; }
}
