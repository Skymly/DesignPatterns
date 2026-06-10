using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DesignPatterns.Structural;

/// <summary>
/// Traverses composite trees synchronously or asynchronously.
/// </summary>
public static class CompositeTraverser
{
    /// <summary>
    /// Traverses the tree in depth-first pre-order.
    /// </summary>
    public static void Traverse<TNode>(
        TNode root,
        Action<TNode, int, int> visitor,
        CompositeTraversalOptions<TNode>? options = null)
        where TNode : ICompositeNode<TNode>
    {
        if (root is null)
        {
            throw new ArgumentNullException(nameof(root));
        }

        if (visitor is null)
        {
            throw new ArgumentNullException(nameof(visitor));
        }

        options ??= new CompositeTraversalOptions<TNode>();
        TraverseCore(root, visitor, options, CancellationToken.None);
    }

    /// <summary>
    /// Traverses a forest (multiple root trees) synchronously.
    /// </summary>
    /// <param name="roots">Root nodes to traverse, in forest order.</param>
    /// <param name="visitor">Callback invoked for each visited node (node, depth, siblingIndex).</param>
    /// <param name="options">Optional traversal options.</param>
    public static void TraverseForest<TNode>(
        IReadOnlyList<TNode> roots,
        Action<TNode, int, int> visitor,
        CompositeTraversalOptions<TNode>? options = null)
        where TNode : ICompositeNode<TNode>
    {
        if (roots is null)
        {
            throw new ArgumentNullException(nameof(roots));
        }

        if (visitor is null)
        {
            throw new ArgumentNullException(nameof(visitor));
        }

        options ??= new CompositeTraversalOptions<TNode>();
        TraverseForestCore(roots, visitor, options, CancellationToken.None);
    }

    /// <summary>
    /// Traverses the tree asynchronously.
    /// </summary>
    public static async ValueTask TraverseAsync<TNode>(
        TNode root,
        Func<TNode, int, int, CancellationToken, ValueTask> visitor,
        CompositeTraversalOptions<TNode>? options = null,
        CancellationToken cancellationToken = default)
        where TNode : ICompositeNode<TNode>
    {
        if (root is null)
        {
            throw new ArgumentNullException(nameof(root));
        }

        if (visitor is null)
        {
            throw new ArgumentNullException(nameof(visitor));
        }

        options ??= new CompositeTraversalOptions<TNode>();
        await TraverseAsyncCore(root, visitor, options, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Traverses a forest (multiple root trees) asynchronously.
    /// </summary>
    /// <param name="roots">Root nodes to traverse, in forest order.</param>
    /// <param name="visitor">Async callback invoked for each visited node (node, depth, siblingIndex, cancellationToken).</param>
    /// <param name="options">Optional traversal options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async ValueTask TraverseForestAsync<TNode>(
        IReadOnlyList<TNode> roots,
        Func<TNode, int, int, CancellationToken, ValueTask> visitor,
        CompositeTraversalOptions<TNode>? options = null,
        CancellationToken cancellationToken = default)
        where TNode : ICompositeNode<TNode>
    {
        if (roots is null)
        {
            throw new ArgumentNullException(nameof(roots));
        }

        if (visitor is null)
        {
            throw new ArgumentNullException(nameof(visitor));
        }

        options ??= new CompositeTraversalOptions<TNode>();
        await TraverseForestAsyncCore(roots, visitor, options, cancellationToken).ConfigureAwait(false);
    }

    private static void TraverseForestCore<TNode>(
        IReadOnlyList<TNode> roots,
        Action<TNode, int, int> visitor,
        CompositeTraversalOptions<TNode> options,
        CancellationToken cancellationToken)
        where TNode : ICompositeNode<TNode>
    {
        switch (options.Order)
        {
            case CompositeTraversalOrder.DepthFirstPreOrder:
                for (var i = 0; i < roots.Count; i++)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    DepthFirstPreOrderWithSibling(roots[i], visitor, options, cancellationToken, depth: 0, i);
                }

                break;
            case CompositeTraversalOrder.DepthFirstPostOrder:
                for (var i = 0; i < roots.Count; i++)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    DepthFirstPostOrderWithSibling(roots[i], visitor, options, cancellationToken, depth: 0, i);
                }

                break;
            case CompositeTraversalOrder.BreadthFirst:
                BreadthFirstForest(roots, visitor, options, cancellationToken);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(options.Order));
        }
    }

    private static void TraverseCore<TNode>(
        TNode root,
        Action<TNode, int, int> visitor,
        CompositeTraversalOptions<TNode> options,
        CancellationToken cancellationToken)
        where TNode : ICompositeNode<TNode>
    {
        switch (options.Order)
        {
            case CompositeTraversalOrder.DepthFirstPreOrder:
                DepthFirstPreOrder(root, visitor, options, cancellationToken, depth: 0);
                break;
            case CompositeTraversalOrder.DepthFirstPostOrder:
                DepthFirstPostOrder(root, visitor, options, cancellationToken, depth: 0);
                break;
            case CompositeTraversalOrder.BreadthFirst:
                BreadthFirst(root, visitor, options, cancellationToken);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(options.Order));
        }
    }

    private static async ValueTask TraverseForestAsyncCore<TNode>(
        IReadOnlyList<TNode> roots,
        Func<TNode, int, int, CancellationToken, ValueTask> visitor,
        CompositeTraversalOptions<TNode> options,
        CancellationToken cancellationToken)
        where TNode : ICompositeNode<TNode>
    {
        switch (options.Order)
        {
            case CompositeTraversalOrder.DepthFirstPreOrder:
                for (var i = 0; i < roots.Count; i++)
                {
                    await DepthFirstPreOrderWithSiblingAsync(
                            roots[i],
                            visitor,
                            options,
                            cancellationToken,
                            depth: 0,
                            i)
                        .ConfigureAwait(false);

                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }
                }

                break;
            case CompositeTraversalOrder.DepthFirstPostOrder:
                for (var i = 0; i < roots.Count; i++)
                {
                    await DepthFirstPostOrderWithSiblingAsync(
                            roots[i],
                            visitor,
                            options,
                            cancellationToken,
                            depth: 0,
                            i)
                        .ConfigureAwait(false);

                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }
                }

                break;
            case CompositeTraversalOrder.BreadthFirst:
                await BreadthFirstForestAsync(roots, visitor, options, cancellationToken).ConfigureAwait(false);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(options.Order));
        }
    }

    private static async ValueTask TraverseAsyncCore<TNode>(
        TNode root,
        Func<TNode, int, int, CancellationToken, ValueTask> visitor,
        CompositeTraversalOptions<TNode> options,
        CancellationToken cancellationToken)
        where TNode : ICompositeNode<TNode>
    {
        switch (options.Order)
        {
            case CompositeTraversalOrder.DepthFirstPreOrder:
                await DepthFirstPreOrderAsync(root, visitor, options, cancellationToken, depth: 0)
                    .ConfigureAwait(false);
                break;
            case CompositeTraversalOrder.DepthFirstPostOrder:
                await DepthFirstPostOrderAsync(root, visitor, options, cancellationToken, depth: 0)
                    .ConfigureAwait(false);
                break;
            case CompositeTraversalOrder.BreadthFirst:
                await BreadthFirstAsync(root, visitor, options, cancellationToken).ConfigureAwait(false);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(options.Order));
        }
    }

    private static void DepthFirstPreOrder<TNode>(
        TNode node,
        Action<TNode, int, int> visitor,
        CompositeTraversalOptions<TNode> options,
        CancellationToken cancellationToken,
        int depth)
        where TNode : ICompositeNode<TNode>
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        if (options.MaxDepth.HasValue && depth > options.MaxDepth.Value)
        {
            return;
        }

        if (options.ShouldSkipSubtree?.Invoke(node) == true)
        {
            return;
        }

        if (!options.VisitLeavesOnly || node.IsLeaf())
        {
            visitor(node, depth, 0);
        }

        var children = node.Children;
        for (var i = 0; i < children.Count; i++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            DepthFirstPreOrderWithSibling(children[i], visitor, options, cancellationToken, depth + 1, i);
        }
    }

    private static void DepthFirstPreOrderWithSibling<TNode>(
        TNode node,
        Action<TNode, int, int> visitor,
        CompositeTraversalOptions<TNode> options,
        CancellationToken cancellationToken,
        int depth,
        int siblingIndex)
        where TNode : ICompositeNode<TNode>
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        if (options.MaxDepth.HasValue && depth > options.MaxDepth.Value)
        {
            return;
        }

        if (options.ShouldSkipSubtree?.Invoke(node) == true)
        {
            return;
        }

        if (!options.VisitLeavesOnly || node.IsLeaf())
        {
            visitor(node, depth, siblingIndex);
        }

        var children = node.Children;
        for (var i = 0; i < children.Count; i++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            DepthFirstPreOrderWithSibling(children[i], visitor, options, cancellationToken, depth + 1, i);
        }
    }

    private static void DepthFirstPostOrder<TNode>(
        TNode node,
        Action<TNode, int, int> visitor,
        CompositeTraversalOptions<TNode> options,
        CancellationToken cancellationToken,
        int depth)
        where TNode : ICompositeNode<TNode>
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        if (options.MaxDepth.HasValue && depth > options.MaxDepth.Value)
        {
            return;
        }

        if (options.ShouldSkipSubtree?.Invoke(node) == true)
        {
            return;
        }

        var children = node.Children;
        for (var i = 0; i < children.Count; i++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            DepthFirstPostOrderWithSibling(children[i], visitor, options, cancellationToken, depth + 1, i);
        }

        if (!options.VisitLeavesOnly || node.IsLeaf())
        {
            visitor(node, depth, 0);
        }
    }

    private static void DepthFirstPostOrderWithSibling<TNode>(
        TNode node,
        Action<TNode, int, int> visitor,
        CompositeTraversalOptions<TNode> options,
        CancellationToken cancellationToken,
        int depth,
        int siblingIndex)
        where TNode : ICompositeNode<TNode>
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        if (options.MaxDepth.HasValue && depth > options.MaxDepth.Value)
        {
            return;
        }

        if (options.ShouldSkipSubtree?.Invoke(node) == true)
        {
            return;
        }

        var children = node.Children;
        for (var i = 0; i < children.Count; i++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            DepthFirstPostOrderWithSibling(children[i], visitor, options, cancellationToken, depth + 1, i);
        }

        if (!options.VisitLeavesOnly || node.IsLeaf())
        {
            visitor(node, depth, siblingIndex);
        }
    }

    private static void BreadthFirst<TNode>(
        TNode root,
        Action<TNode, int, int> visitor,
        CompositeTraversalOptions<TNode> options,
        CancellationToken cancellationToken)
        where TNode : ICompositeNode<TNode>
    {
        var queue = new Queue<(TNode Node, int Depth, int SiblingIndex)>();
        queue.Enqueue((root, 0, 0));
        BreadthFirstCore(queue, visitor, options, cancellationToken);
    }

    private static void BreadthFirstForest<TNode>(
        IReadOnlyList<TNode> roots,
        Action<TNode, int, int> visitor,
        CompositeTraversalOptions<TNode> options,
        CancellationToken cancellationToken)
        where TNode : ICompositeNode<TNode>
    {
        var queue = new Queue<(TNode Node, int Depth, int SiblingIndex)>();
        for (var i = 0; i < roots.Count; i++)
        {
            queue.Enqueue((roots[i], 0, i));
        }

        BreadthFirstCore(queue, visitor, options, cancellationToken);
    }

    private static void BreadthFirstCore<TNode>(
        Queue<(TNode Node, int Depth, int SiblingIndex)> queue,
        Action<TNode, int, int> visitor,
        CompositeTraversalOptions<TNode> options,
        CancellationToken cancellationToken)
        where TNode : ICompositeNode<TNode>
    {
        while (queue.Count > 0)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            var (node, depth, siblingIndex) = queue.Dequeue();

            if (options.MaxDepth.HasValue && depth > options.MaxDepth.Value)
            {
                continue;
            }

            if (options.ShouldSkipSubtree?.Invoke(node) == true)
            {
                continue;
            }

            if (!options.VisitLeavesOnly || node.IsLeaf())
            {
                visitor(node, depth, siblingIndex);
            }

            var children = node.Children;
            for (var i = 0; i < children.Count; i++)
            {
                queue.Enqueue((children[i], depth + 1, i));
            }
        }
    }

    private static async ValueTask DepthFirstPreOrderAsync<TNode>(
        TNode node,
        Func<TNode, int, int, CancellationToken, ValueTask> visitor,
        CompositeTraversalOptions<TNode> options,
        CancellationToken cancellationToken,
        int depth)
        where TNode : ICompositeNode<TNode>
    {
        await DepthFirstPreOrderWithSiblingAsync(node, visitor, options, cancellationToken, depth, siblingIndex: 0)
            .ConfigureAwait(false);
    }

    private static async ValueTask DepthFirstPreOrderWithSiblingAsync<TNode>(
        TNode node,
        Func<TNode, int, int, CancellationToken, ValueTask> visitor,
        CompositeTraversalOptions<TNode> options,
        CancellationToken cancellationToken,
        int depth,
        int siblingIndex)
        where TNode : ICompositeNode<TNode>
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        if (options.MaxDepth.HasValue && depth > options.MaxDepth.Value)
        {
            return;
        }

        if (options.ShouldSkipSubtree?.Invoke(node) == true)
        {
            return;
        }

        if (!options.VisitLeavesOnly || node.IsLeaf())
        {
            await visitor(node, depth, siblingIndex, cancellationToken).ConfigureAwait(false);
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        var children = node.Children;
        for (var i = 0; i < children.Count; i++)
        {
            await DepthFirstPreOrderWithSiblingAsync(
                    children[i],
                    visitor,
                    options,
                    cancellationToken,
                    depth + 1,
                    i)
                .ConfigureAwait(false);

            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }
        }
    }

    private static async ValueTask DepthFirstPostOrderAsync<TNode>(
        TNode node,
        Func<TNode, int, int, CancellationToken, ValueTask> visitor,
        CompositeTraversalOptions<TNode> options,
        CancellationToken cancellationToken,
        int depth)
        where TNode : ICompositeNode<TNode>
    {
        await DepthFirstPostOrderWithSiblingAsync(node, visitor, options, cancellationToken, depth, siblingIndex: 0)
            .ConfigureAwait(false);
    }

    private static async ValueTask DepthFirstPostOrderWithSiblingAsync<TNode>(
        TNode node,
        Func<TNode, int, int, CancellationToken, ValueTask> visitor,
        CompositeTraversalOptions<TNode> options,
        CancellationToken cancellationToken,
        int depth,
        int siblingIndex)
        where TNode : ICompositeNode<TNode>
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        if (options.MaxDepth.HasValue && depth > options.MaxDepth.Value)
        {
            return;
        }

        if (options.ShouldSkipSubtree?.Invoke(node) == true)
        {
            return;
        }

        var children = node.Children;
        for (var i = 0; i < children.Count; i++)
        {
            await DepthFirstPostOrderWithSiblingAsync(
                    children[i],
                    visitor,
                    options,
                    cancellationToken,
                    depth + 1,
                    i)
                .ConfigureAwait(false);

            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }
        }

        if (!options.VisitLeavesOnly || node.IsLeaf())
        {
            await visitor(node, depth, siblingIndex, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async ValueTask BreadthFirstAsync<TNode>(
        TNode root,
        Func<TNode, int, int, CancellationToken, ValueTask> visitor,
        CompositeTraversalOptions<TNode> options,
        CancellationToken cancellationToken)
        where TNode : ICompositeNode<TNode>
    {
        var queue = new Queue<(TNode Node, int Depth, int SiblingIndex)>();
        queue.Enqueue((root, 0, 0));
        await BreadthFirstCoreAsync(queue, visitor, options, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask BreadthFirstForestAsync<TNode>(
        IReadOnlyList<TNode> roots,
        Func<TNode, int, int, CancellationToken, ValueTask> visitor,
        CompositeTraversalOptions<TNode> options,
        CancellationToken cancellationToken)
        where TNode : ICompositeNode<TNode>
    {
        var queue = new Queue<(TNode Node, int Depth, int SiblingIndex)>();
        for (var i = 0; i < roots.Count; i++)
        {
            queue.Enqueue((roots[i], 0, i));
        }

        await BreadthFirstCoreAsync(queue, visitor, options, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask BreadthFirstCoreAsync<TNode>(
        Queue<(TNode Node, int Depth, int SiblingIndex)> queue,
        Func<TNode, int, int, CancellationToken, ValueTask> visitor,
        CompositeTraversalOptions<TNode> options,
        CancellationToken cancellationToken)
        where TNode : ICompositeNode<TNode>
    {
        while (queue.Count > 0)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            var (node, depth, siblingIndex) = queue.Dequeue();

            if (options.MaxDepth.HasValue && depth > options.MaxDepth.Value)
            {
                continue;
            }

            if (options.ShouldSkipSubtree?.Invoke(node) == true)
            {
                continue;
            }

            if (!options.VisitLeavesOnly || node.IsLeaf())
            {
                await visitor(node, depth, siblingIndex, cancellationToken).ConfigureAwait(false);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            var children = node.Children;
            for (var i = 0; i < children.Count; i++)
            {
                queue.Enqueue((children[i], depth + 1, i));
            }
        }
    }
}
