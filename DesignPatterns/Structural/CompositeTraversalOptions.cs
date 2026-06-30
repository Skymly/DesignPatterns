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
    /// <remarks>
    /// For parallel traversal, this callback may be invoked concurrently from multiple threads.
    /// Implementations must ensure thread safety if accessing shared state.
    /// Prefer stateless predicates or use synchronization primitives.
    /// </remarks>
    public Func<TNode, bool>? ShouldSkipSubtree { get; set; }

    /// <summary>
    /// Maximum degree of parallelism for parallel traversals
    /// (<see cref="CompositeTraverser.TraverseParallel{TNode}"/> /
    /// <see cref="CompositeTraverser.TraverseParallelAsync{TNode}"/>).
    /// <see langword="null"/> means <see cref="Environment.ProcessorCount"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// WARNING: Setting to 1 does NOT guarantee visitation order.
    /// Use <see cref="CompositeTraverser.Traverse{TNode}"/> for ordered traversal.
    /// </para>
    /// <para>
    /// In containerized environments (Docker/Kubernetes), <see cref="Environment.ProcessorCount"/>
    /// may return the host CPU count rather than the container limit.
    /// Manually set this value to match the container's CPU quota.
    /// </para>
    /// </remarks>
    public int? MaxDegreeOfParallelism { get; set; }

    /// <summary>
    /// Maximum depth for parallel recursion. Beyond this depth, traversal
    /// falls back to sequential to avoid excessive task creation.
    /// Default is 32.
    /// </summary>
    public int MaxParallelDepth { get; set; } = 32;
}
