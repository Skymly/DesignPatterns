using System;
using System.Collections.Generic;

namespace DesignPatterns.Structural;

/// <summary>
/// Builds immutable composite trees without catalog metadata.
/// </summary>
/// <typeparam name="TNode">The node type.</typeparam>
public sealed class CompositeTreeBuilder<TNode>
    where TNode : class, ICompositeNode<TNode>
{
    private readonly List<TNode> _children = new();

    /// <summary>
    /// Adds a leaf node.
    /// </summary>
    public CompositeTreeBuilder<TNode> Leaf(TNode node)
    {
        if (node is null)
        {
            throw new ArgumentNullException(nameof(node));
        }

        _children.Add(node);
        return this;
    }

    /// <summary>
    /// Adds a branch built from a nested builder.
    /// </summary>
    public CompositeTreeBuilder<TNode> Branch(TNode node, Action<CompositeTreeBuilder<TNode>> configure)
    {
        if (node is null)
        {
            throw new ArgumentNullException(nameof(node));
        }

        if (configure is null)
        {
            throw new ArgumentNullException(nameof(configure));
        }

        var nested = new CompositeTreeBuilder<TNode>();
        configure(nested);
        ApplyChildren(node, nested._children);
        _children.Add(node);
        return this;
    }

    /// <summary>
    /// Builds a single root from exactly one top-level node added via <see cref="Leaf"/> or <see cref="Branch"/>.
    /// </summary>
    public TNode Build()
    {
        if (_children.Count != 1)
        {
            throw new InvalidOperationException("CompositeTreeBuilder requires exactly one root node.");
        }

        return _children[0];
    }

    private static void ApplyChildren(TNode node, IReadOnlyList<TNode> children)
    {
        if (node is ICompositeBuildable<TNode> buildable)
        {
            buildable.SetChildren(children);
            return;
        }

        throw new InvalidOperationException(
            $"Node type '{node.GetType().FullName}' must implement ICompositeBuildable<{typeof(TNode).Name}> to receive children.");
    }
}
