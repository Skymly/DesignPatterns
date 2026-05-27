using System;

namespace DesignPatterns.Structural;

/// <summary>
/// Options controlling composite tree traversal.
/// </summary>
/// <typeparam name="TNode">The node type.</typeparam>
public sealed class CompositeTraversalOptions<TNode>
    where TNode : ICompositeNode<TNode>
{
    /// <summary>
    /// Traversal order. Default is <see cref="CompositeTraversalOrder.DepthFirstPreOrder"/>.
    /// </summary>
    public CompositeTraversalOrder Order { get; set; } = CompositeTraversalOrder.DepthFirstPreOrder;

    /// <summary>
    /// Maximum depth to visit (0 = root only). <see langword="null"/> means no limit.
    /// </summary>
    public int? MaxDepth { get; set; }

    /// <summary>
    /// When <see langword="true"/>, only leaf nodes are passed to the visitor.
    /// </summary>
    public bool VisitLeavesOnly { get; set; }

    /// <summary>
    /// When this returns <see langword="true"/>, the node's subtree is skipped.
    /// </summary>
    public Func<TNode, bool>? ShouldSkipSubtree { get; set; }
}
