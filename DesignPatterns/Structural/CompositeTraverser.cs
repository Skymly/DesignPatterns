using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
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

    /// <summary>
    /// Traverses the tree in parallel using the specified degree of parallelism.
    /// </summary>
    /// <param name="root">The root node.</param>
    /// <param name="visitor">
    /// Callback invoked for each visited node (node, depth, siblingIndex).
    /// In parallel mode, this may be called concurrently from multiple threads.
    /// Implementations must ensure thread safety if accessing shared state.
    /// Use <see cref="ConcurrentBag{T}"/> for accumulation, <c>lock</c>/<see cref="SemaphoreSlim"/> for shared state.
    /// </param>
    /// <param name="options">Optional traversal options. <see cref="CompositeTraversalOptions{TNode}.MaxDegreeOfParallelism"/> controls concurrency.</param>
    /// <remarks>
    /// <para>
    /// The visitation order is non-deterministic. If order matters,
    /// use <see cref="Traverse{TNode}"/> or <see cref="TraverseAsync{TNode}"/>.
    /// </para>
    /// <para>
    /// Exceptions from visitor callbacks are collected and rethrown as <see cref="AggregateException"/>.
    /// </para>
    /// </remarks>
    public static void TraverseParallel<TNode>(
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
        var maxDop = options.MaxDegreeOfParallelism ?? Environment.ProcessorCount;
        var exceptions = new ConcurrentQueue<Exception>();
        TraverseParallelCore(root, visitor, options, maxDop, exceptions);
        ThrowIfExceptions(exceptions);
    }

    /// <summary>
    /// Traverses a forest (multiple root trees) in parallel.
    /// </summary>
    /// <param name="roots">Root nodes to traverse.</param>
    /// <param name="visitor">
    /// Callback invoked for each visited node. In parallel mode, this may be called
    /// concurrently from multiple threads. See <see cref="TraverseParallel{TNode}"/> for thread-safety requirements.
    /// </param>
    /// <param name="options">Optional traversal options.</param>
    public static void TraverseForestParallel<TNode>(
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
        var maxDop = options.MaxDegreeOfParallelism ?? Environment.ProcessorCount;
        var exceptions = new ConcurrentQueue<Exception>();

        for (var i = 0; i < roots.Count; i++)
        {
            TraverseParallelCore(roots[i], visitor, options, maxDop, exceptions, rootSiblingIndex: i);
        }

        ThrowIfExceptions(exceptions);
    }

    /// <summary>
    /// Traverses the tree in parallel asynchronously.
    /// </summary>
    /// <param name="root">The root node.</param>
    /// <param name="visitor">
    /// Async callback invoked for each visited node. In parallel mode, this may be called
    /// concurrently from multiple threads. See <see cref="TraverseParallel{TNode}"/> for thread-safety requirements.
    /// </param>
    /// <param name="options">Optional traversal options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async ValueTask TraverseParallelAsync<TNode>(
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
        var maxDop = options.MaxDegreeOfParallelism ?? Environment.ProcessorCount;
        var exceptions = new ConcurrentQueue<Exception>();
        await TraverseParallelAsyncCore(root, visitor, options, maxDop, exceptions, cancellationToken)
            .ConfigureAwait(false);
        ThrowIfExceptions(exceptions);
    }

    /// <summary>
    /// Traverses a forest (multiple root trees) in parallel asynchronously.
    /// </summary>
    /// <param name="roots">Root nodes to traverse.</param>
    /// <param name="visitor">
    /// Async callback invoked for each visited node. In parallel mode, this may be called
    /// concurrently from multiple threads. See <see cref="TraverseParallel{TNode}"/> for thread-safety requirements.
    /// </param>
    /// <param name="options">Optional traversal options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async ValueTask TraverseForestParallelAsync<TNode>(
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
        var maxDop = options.MaxDegreeOfParallelism ?? Environment.ProcessorCount;
        var exceptions = new ConcurrentQueue<Exception>();

        for (var i = 0; i < roots.Count; i++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            await TraverseParallelAsyncCore(
                    roots[i], visitor, options, maxDop, exceptions, cancellationToken, rootSiblingIndex: i)
                .ConfigureAwait(false);
        }

        ThrowIfExceptions(exceptions);
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

    // ─── Parallel sync core ───────────────────────────────────────────

    private static void TraverseParallelCore<TNode>(
        TNode root,
        Action<TNode, int, int> visitor,
        CompositeTraversalOptions<TNode> options,
        int maxDop,
        ConcurrentQueue<Exception> exceptions,
        int rootSiblingIndex = 0)
        where TNode : ICompositeNode<TNode>
    {
        switch (options.Order)
        {
            case CompositeTraversalOrder.DepthFirstPreOrder:
                DepthFirstPreOrderParallel(root, visitor, options, maxDop, exceptions, depth: 0, rootSiblingIndex);
                break;
            case CompositeTraversalOrder.DepthFirstPostOrder:
                DepthFirstPostOrderParallel(root, visitor, options, maxDop, exceptions, depth: 0, rootSiblingIndex);
                break;
            case CompositeTraversalOrder.BreadthFirst:
                BreadthFirstParallel(root, visitor, options, maxDop, exceptions, rootSiblingIndex);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(options.Order));
        }
    }

    private static void DepthFirstPreOrderParallel<TNode>(
        TNode node,
        Action<TNode, int, int> visitor,
        CompositeTraversalOptions<TNode> options,
        int maxDop,
        ConcurrentQueue<Exception> exceptions,
        int depth,
        int siblingIndex)
        where TNode : ICompositeNode<TNode>
    {
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
            try { visitor(node, depth, siblingIndex); }
            catch (Exception ex) { exceptions.Enqueue(ex); }
        }

        var children = node.Children;
        if (children.Count == 0)
        {
            return;
        }

        // Beyond MaxParallelDepth, fall back to sequential to avoid excessive task creation.
        if (depth >= options.MaxParallelDepth)
        {
            for (var i = 0; i < children.Count; i++)
            {
                DepthFirstPreOrderParallel(children[i], visitor, options, maxDop, exceptions, depth + 1, i);
            }
            return;
        }

        var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = maxDop };
        Parallel.ForEach(children, parallelOptions, (child, _, i) =>
        {
            DepthFirstPreOrderParallel(child, visitor, options, maxDop, exceptions, depth + 1, (int)i);
        });
    }

    private static void DepthFirstPostOrderParallel<TNode>(
        TNode node,
        Action<TNode, int, int> visitor,
        CompositeTraversalOptions<TNode> options,
        int maxDop,
        ConcurrentQueue<Exception> exceptions,
        int depth,
        int siblingIndex)
        where TNode : ICompositeNode<TNode>
    {
        if (options.MaxDepth.HasValue && depth > options.MaxDepth.Value)
        {
            return;
        }

        if (options.ShouldSkipSubtree?.Invoke(node) == true)
        {
            return;
        }

        var children = node.Children;
        if (children.Count > 0)
        {
            if (depth >= options.MaxParallelDepth)
            {
                for (var i = 0; i < children.Count; i++)
                {
                    DepthFirstPostOrderParallel(children[i], visitor, options, maxDop, exceptions, depth + 1, i);
                }
            }
            else
            {
                var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = maxDop };
                Parallel.ForEach(children, parallelOptions, (child, _, i) =>
                {
                    DepthFirstPostOrderParallel(child, visitor, options, maxDop, exceptions, depth + 1, (int)i);
                });
            }
        }

        if (!options.VisitLeavesOnly || node.IsLeaf())
        {
            try { visitor(node, depth, siblingIndex); }
            catch (Exception ex) { exceptions.Enqueue(ex); }
        }
    }

    private static void BreadthFirstParallel<TNode>(
        TNode root,
        Action<TNode, int, int> visitor,
        CompositeTraversalOptions<TNode> options,
        int maxDop,
        ConcurrentQueue<Exception> exceptions,
        int rootSiblingIndex)
        where TNode : ICompositeNode<TNode>
    {
        var queue = new Queue<(TNode Node, int Depth, int SiblingIndex)>();
        queue.Enqueue((root, 0, rootSiblingIndex));

        while (queue.Count > 0)
        {
            // Extract current level.
            var currentLevel = new List<(TNode Node, int Depth, int SiblingIndex)>(queue.Count);
            while (queue.Count > 0)
            {
                currentLevel.Add(queue.Dequeue());
            }

            var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = maxDop };
            var nextQueue = new Queue<(TNode Node, int Depth, int SiblingIndex)>();
            Parallel.ForEach(currentLevel, parallelOptions, item =>
            {
                var (node, depth, siblingIndex) = item;

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
                    try { visitor(node, depth, siblingIndex); }
                    catch (Exception ex) { exceptions.Enqueue(ex); }
                }

                var children = node.Children;
                for (var i = 0; i < children.Count; i++)
                {
                    lock (nextQueue)
                    {
                        nextQueue.Enqueue((children[i], depth + 1, i));
                    }
                }
            });

            queue = nextQueue;
        }
    }

    // ─── Parallel async core ──────────────────────────────────────────

    private static async ValueTask TraverseParallelAsyncCore<TNode>(
        TNode root,
        Func<TNode, int, int, CancellationToken, ValueTask> visitor,
        CompositeTraversalOptions<TNode> options,
        int maxDop,
        ConcurrentQueue<Exception> exceptions,
        CancellationToken cancellationToken,
        int rootSiblingIndex = 0)
        where TNode : ICompositeNode<TNode>
    {
        switch (options.Order)
        {
            case CompositeTraversalOrder.DepthFirstPreOrder:
                await DepthFirstPreOrderParallelAsync(root, visitor, options, maxDop, exceptions, cancellationToken, depth: 0, rootSiblingIndex)
                    .ConfigureAwait(false);
                break;
            case CompositeTraversalOrder.DepthFirstPostOrder:
                await DepthFirstPostOrderParallelAsync(root, visitor, options, maxDop, exceptions, cancellationToken, depth: 0, rootSiblingIndex)
                    .ConfigureAwait(false);
                break;
            case CompositeTraversalOrder.BreadthFirst:
                await BreadthFirstParallelAsync(root, visitor, options, maxDop, exceptions, cancellationToken, rootSiblingIndex)
                    .ConfigureAwait(false);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(options.Order));
        }
    }

    private static async ValueTask DepthFirstPreOrderParallelAsync<TNode>(
        TNode node,
        Func<TNode, int, int, CancellationToken, ValueTask> visitor,
        CompositeTraversalOptions<TNode> options,
        int maxDop,
        ConcurrentQueue<Exception> exceptions,
        CancellationToken cancellationToken,
        int depth,
        int siblingIndex)
        where TNode : ICompositeNode<TNode>
    {
        if (cancellationToken.IsCancellationRequested || options.MaxDepth.HasValue && depth > options.MaxDepth.Value)
        {
            return;
        }

        if (options.ShouldSkipSubtree?.Invoke(node) == true)
        {
            return;
        }

        if (!options.VisitLeavesOnly || node.IsLeaf())
        {
            try { await visitor(node, depth, siblingIndex, cancellationToken).ConfigureAwait(false); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return; }
            catch (Exception ex) { exceptions.Enqueue(ex); }
        }

        var children = node.Children;
        if (children.Count == 0)
        {
            return;
        }

        // Beyond MaxParallelDepth, fall back to sequential.
        if (depth >= options.MaxParallelDepth)
        {
            for (var i = 0; i < children.Count; i++)
            {
                if (cancellationToken.IsCancellationRequested) return;
                await DepthFirstPreOrderParallelAsync(children[i], visitor, options, maxDop, exceptions, cancellationToken, depth + 1, i)
                    .ConfigureAwait(false);
            }
            return;
        }

        try
        {
            await ForEachParallelAsync(children, maxDop, cancellationToken, async (child, i) =>
            {
                await DepthFirstPreOrderParallelAsync(child, visitor, options, maxDop, exceptions, cancellationToken, depth + 1, i)
                    .ConfigureAwait(false);
            }).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return; }
    }

    private static async ValueTask DepthFirstPostOrderParallelAsync<TNode>(
        TNode node,
        Func<TNode, int, int, CancellationToken, ValueTask> visitor,
        CompositeTraversalOptions<TNode> options,
        int maxDop,
        ConcurrentQueue<Exception> exceptions,
        CancellationToken cancellationToken,
        int depth,
        int siblingIndex)
        where TNode : ICompositeNode<TNode>
    {
        if (cancellationToken.IsCancellationRequested || options.MaxDepth.HasValue && depth > options.MaxDepth.Value)
        {
            return;
        }

        if (options.ShouldSkipSubtree?.Invoke(node) == true)
        {
            return;
        }

        var children = node.Children;
        if (children.Count > 0)
        {
            if (depth >= options.MaxParallelDepth)
            {
                for (var i = 0; i < children.Count; i++)
                {
                    if (cancellationToken.IsCancellationRequested) return;
                    await DepthFirstPostOrderParallelAsync(children[i], visitor, options, maxDop, exceptions, cancellationToken, depth + 1, i)
                        .ConfigureAwait(false);
                }
            }
            else
            {
                try
                {
                    await ForEachParallelAsync(children, maxDop, cancellationToken, async (child, i) =>
                    {
                        await DepthFirstPostOrderParallelAsync(child, visitor, options, maxDop, exceptions, cancellationToken, depth + 1, i)
                            .ConfigureAwait(false);
                    }).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return; }
            }
        }

        if (cancellationToken.IsCancellationRequested) return;

        if (!options.VisitLeavesOnly || node.IsLeaf())
        {
            try { await visitor(node, depth, siblingIndex, cancellationToken).ConfigureAwait(false); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return; }
            catch (Exception ex) { exceptions.Enqueue(ex); }
        }
    }

    private static async ValueTask BreadthFirstParallelAsync<TNode>(
        TNode root,
        Func<TNode, int, int, CancellationToken, ValueTask> visitor,
        CompositeTraversalOptions<TNode> options,
        int maxDop,
        ConcurrentQueue<Exception> exceptions,
        CancellationToken cancellationToken,
        int rootSiblingIndex)
        where TNode : ICompositeNode<TNode>
    {
        var queue = new Queue<(TNode Node, int Depth, int SiblingIndex)>();
        queue.Enqueue((root, 0, rootSiblingIndex));

        while (queue.Count > 0)
        {
            if (cancellationToken.IsCancellationRequested) return;

            var currentLevel = new List<(TNode Node, int Depth, int SiblingIndex)>(queue.Count);
            while (queue.Count > 0)
            {
                currentLevel.Add(queue.Dequeue());
            }

            var nextQueue = new Queue<(TNode Node, int Depth, int SiblingIndex)>();
            try
            {
                await ForEachParallelAsync(currentLevel, maxDop, cancellationToken, async (item, _) =>
                {
                    var (node, depth, siblingIndex) = item;

                    if (options.MaxDepth.HasValue && depth > options.MaxDepth.Value) return;
                    if (options.ShouldSkipSubtree?.Invoke(node) == true) return;

                    if (!options.VisitLeavesOnly || node.IsLeaf())
                    {
                        try { await visitor(node, depth, siblingIndex, cancellationToken).ConfigureAwait(false); }
                        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return; }
                        catch (Exception ex) { exceptions.Enqueue(ex); }
                    }

                    var children = node.Children;
                    for (var i = 0; i < children.Count; i++)
                    {
                        lock (nextQueue)
                        {
                            nextQueue.Enqueue((children[i], depth + 1, i));
                        }
                    }
                }).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return; }

            queue = nextQueue;
        }
    }

    // ─── Parallel async helper ────────────────────────────────────────

    /// <summary>
    /// Invokes <paramref name="body"/> for each item in <paramref name="source"/> with
    /// at most <paramref name="maxDop"/> concurrent operations. Uses
    /// <see cref="Parallel.ForEachAsync"/> on .NET 6+ and <see cref="SemaphoreSlim"/>
    /// on netstandard2.0.
    /// </summary>
    private static async ValueTask ForEachParallelAsync<T>(
        IReadOnlyList<T> source,
        int maxDop,
        CancellationToken cancellationToken,
        Func<T, int, ValueTask> body)
    {
#if NET8_0_OR_GREATER
        await Parallel.ForEachAsync(
            source.Select((item, index) => (item, index)),
            new ParallelOptions
            {
                MaxDegreeOfParallelism = maxDop,
                CancellationToken = cancellationToken,
            },
            async (pair, ct) =>
            {
                await body(pair.item, pair.index).ConfigureAwait(false);
            }).ConfigureAwait(false);
#else
        var semaphore = new SemaphoreSlim(maxDop, maxDop);
        var tasks = new List<Task>(source.Count);

        for (var i = 0; i < source.Count; i++)
        {
            if (cancellationToken.IsCancellationRequested) break;

            var item = source[i];
            var index = i;

            await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    await body(item, index).ConfigureAwait(false);
                }
                finally
                {
                    semaphore.Release();
                }
            }, cancellationToken));
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);
#endif
    }

    // ─── Exception helper ─────────────────────────────────────────────

    private static void ThrowIfExceptions(ConcurrentQueue<Exception> exceptions)
    {
        if (!exceptions.IsEmpty)
        {
            throw new AggregateException(exceptions);
        }
    }
}
