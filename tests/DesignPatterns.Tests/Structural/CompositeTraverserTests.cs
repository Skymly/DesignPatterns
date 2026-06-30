using System.Collections.Concurrent;
using DesignPatterns.Structural;

namespace DesignPatterns.Tests.Structural;

public sealed class CompositeTraverserTests
{
    private interface ITestNode : ICompositeNode<ITestNode>
    {
        string Name { get; }
    }

    private sealed class TestNode : ITestNode, ICompositeBuildable<ITestNode>
    {
        private IReadOnlyList<ITestNode> _children = Array.Empty<ITestNode>();

        public TestNode(string name) => Name = name;

        public string Name { get; }

        public IReadOnlyList<ITestNode> Children => _children;

        public void SetChildren(IReadOnlyList<ITestNode> children) =>
            _children = children.ToList();
    }

    private sealed class RootCatalogNode : ITestNode, ICompositeBuildable<ITestNode>
    {
        private IReadOnlyList<ITestNode> _children = Array.Empty<ITestNode>();

        public string Name => "root";

        public IReadOnlyList<ITestNode> Children => _children;

        public void SetChildren(IReadOnlyList<ITestNode> children) =>
            _children = children.ToList();
    }

    private sealed class NodeA : ITestNode, ICompositeBuildable<ITestNode>
    {
        private IReadOnlyList<ITestNode> _children = Array.Empty<ITestNode>();

        public string Name => "a";

        public IReadOnlyList<ITestNode> Children => _children;

        public void SetChildren(IReadOnlyList<ITestNode> children) =>
            _children = children.ToList();
    }

    private sealed class NodeB : ITestNode, ICompositeBuildable<ITestNode>
    {
        private IReadOnlyList<ITestNode> _children = Array.Empty<ITestNode>();

        public string Name => "b";

        public IReadOnlyList<ITestNode> Children => _children;

        public void SetChildren(IReadOnlyList<ITestNode> children) =>
            _children = children.ToList();
    }

    [Fact]
    public void Traverse_DepthFirstPreOrder_VisitsInExpectedOrder()
    {
        var root = BuildSampleTree();

        var visited = new List<string>();
        CompositeTraverser.Traverse(root, (node, _, _) => visited.Add(node.Name));

        Assert.Equal(new[] { "root", "child-a", "grandchild", "child-b" }, visited);
    }

    [Fact]
    public void Traverse_DepthFirstPostOrder_VisitsChildrenBeforeParent()
    {
        var root = BuildSampleTree();

        var visited = new List<string>();
        CompositeTraverser.Traverse(
            root,
            (node, _, _) => visited.Add(node.Name),
            new CompositeTraversalOptions<ITestNode> { Order = CompositeTraversalOrder.DepthFirstPostOrder });

        Assert.Equal(new[] { "grandchild", "child-a", "child-b", "root" }, visited);
    }

    [Fact]
    public void Traverse_BreadthFirst_VisitsLevelByLevel()
    {
        var root = BuildSampleTree();

        var visited = new List<string>();
        CompositeTraverser.Traverse(
            root,
            (node, _, _) => visited.Add(node.Name),
            new CompositeTraversalOptions<ITestNode> { Order = CompositeTraversalOrder.BreadthFirst });

        Assert.Equal(new[] { "root", "child-a", "child-b", "grandchild" }, visited);
    }

    [Fact]
    public void Traverse_MaxDepth_LimitsVisitedNodes()
    {
        var root = BuildSampleTree();

        var visited = new List<string>();
        CompositeTraverser.Traverse(
            root,
            (node, _, _) => visited.Add(node.Name),
            new CompositeTraversalOptions<ITestNode> { MaxDepth = 1 });

        Assert.Equal(new[] { "root", "child-a", "child-b" }, visited);
    }

    [Fact]
    public void Traverse_VisitLeavesOnly_SkipsBranches()
    {
        var root = BuildSampleTree();

        var visited = new List<string>();
        CompositeTraverser.Traverse(
            root,
            (node, _, _) => visited.Add(node.Name),
            new CompositeTraversalOptions<ITestNode> { VisitLeavesOnly = true });

        Assert.Equal(new[] { "grandchild", "child-b" }, visited);
    }

    [Fact]
    public void Traverse_ShouldSkipSubtree_SkipsBranch()
    {
        var root = BuildSampleTree();

        var visited = new List<string>();
        CompositeTraverser.Traverse(
            root,
            (node, _, _) => visited.Add(node.Name),
            new CompositeTraversalOptions<ITestNode>
            {
                ShouldSkipSubtree = node => node.Name == "child-a",
            });

        Assert.Equal(new[] { "root", "child-b" }, visited);
    }

    [Fact]
    public async Task TraverseAsync_CancellationRequested_StopsEarly()
    {
        var root = BuildSampleTree();
        using var cts = new CancellationTokenSource();

        var visited = new List<string>();
        await CompositeTraverser.TraverseAsync(
            root,
            (node, _, _, ct) =>
            {
                visited.Add(node.Name);
                if (node.Name == "child-a")
                {
                    cts.Cancel();
                }

                return default;
            },
            cancellationToken: cts.Token);

        Assert.Equal(new[] { "root", "child-a" }, visited);
    }

    [Fact]
    public void Assemble_BuildsTreeWithOrderedChildren()
    {
        var entries = new[]
        {
            new CompositeCatalogEntry<ITestNode>("root", null, 0, typeof(RootCatalogNode)),
            new CompositeCatalogEntry<ITestNode>("b", "root", 1, typeof(NodeB)),
            new CompositeCatalogEntry<ITestNode>("a", "root", 0, typeof(NodeA)),
        };

        var root = CompositeCatalogAssembler.Assemble(entries);

        Assert.Equal("root", root.Name);
        Assert.Equal(2, root.Children.Count);
        Assert.Equal("a", root.Children[0].Name);
        Assert.Equal("b", root.Children[1].Name);
    }

    [Fact]
    public void Assemble_NoRoot_Throws()
    {
        var entries = new[]
        {
            new CompositeCatalogEntry<ITestNode>("child", "missing", 0, typeof(NodeA)),
        };

        Assert.Throws<CompositeAssemblyException>(() => CompositeCatalogAssembler.Assemble(entries));
    }

    [Fact]
    public void Assemble_MultipleRoots_Throws()
    {
        var entries = new[]
        {
            new CompositeCatalogEntry<ITestNode>("root-a", null, 0, typeof(RootCatalogNode)),
            new CompositeCatalogEntry<ITestNode>("root-b", null, 0, typeof(NodeA)),
        };

        var ex = Assert.Throws<CompositeAssemblyException>(() => CompositeCatalogAssembler.Assemble(entries));
        Assert.Contains("2 root entries", ex.Message);
    }

    [Fact]
    public void TreeBuilder_BuildsSingleRoot()
    {
        var root = new TestNode("root");
        var child = new TestNode("child");

        var built = new CompositeTreeBuilder<ITestNode>()
            .Branch(root, builder => builder.Leaf(child))
            .Build();

        Assert.Same(root, built);
        Assert.Single(built.Children);
        Assert.Same(child, built.Children[0]);
    }

    [Fact]
    public void IsLeaf_ReturnsTrueWhenNoChildren()
    {
        ITestNode node = new TestNode("leaf");

        Assert.True(node.IsLeaf());
    }

    [Fact]
    public void IsLeaf_ReturnsFalseWhenHasChildren()
    {
        var node = new TestNode("parent");
        node.SetChildren(new[] { new TestNode("child") });

        Assert.False(((ITestNode)node).IsLeaf());
    }

    [Fact]
    public void Assemble_EmptyCatalog_ThrowsCompositeAssemblyException()
    {
        var exception = Assert.Throws<CompositeAssemblyException>(
            () => CompositeCatalogAssembler.Assemble(Array.Empty<CompositeCatalogEntry<ITestNode>>()));

        Assert.Contains("empty", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TraverseForest_DepthFirstPreOrder_VisitsAllTreesInForestOrder()
    {
        var forest = BuildSampleForest();
        var visited = new List<string>();
        var rootIndices = new List<int>();

        CompositeTraverser.TraverseForest(
            forest,
            (node, _, siblingIndex) =>
            {
                visited.Add(node.Name);
                if (node.Name is "root-a" or "root-b")
                {
                    rootIndices.Add(siblingIndex);
                }
            });

        Assert.Equal(new[] { "root-a", "child-a", "grandchild-a", "root-b", "child-b" }, visited);
        Assert.Equal(new[] { 0, 1 }, rootIndices);
    }

    [Fact]
    public void TraverseForest_DepthFirstPostOrder_VisitsChildrenBeforeParentsAcrossTrees()
    {
        var forest = BuildSampleForest();
        var visited = new List<string>();

        CompositeTraverser.TraverseForest(
            forest,
            (node, _, _) => visited.Add(node.Name),
            new CompositeTraversalOptions<ITestNode> { Order = CompositeTraversalOrder.DepthFirstPostOrder });

        Assert.Equal(new[] { "grandchild-a", "child-a", "root-a", "child-b", "root-b" }, visited);
    }

    [Fact]
    public void TraverseForest_BreadthFirst_VisitsLevelByLevelAcrossTrees()
    {
        var forest = BuildSampleForest();
        var visited = new List<string>();

        CompositeTraverser.TraverseForest(
            forest,
            (node, _, _) => visited.Add(node.Name),
            new CompositeTraversalOptions<ITestNode> { Order = CompositeTraversalOrder.BreadthFirst });

        Assert.Equal(new[] { "root-a", "root-b", "child-a", "child-b", "grandchild-a" }, visited);
    }

    [Fact]
    public void TraverseForest_EmptyForest_DoesNotInvokeVisitor()
    {
        var visited = new List<string>();

        CompositeTraverser.TraverseForest(
            Array.Empty<ITestNode>(),
            (node, _, _) => visited.Add(node.Name));

        Assert.Empty(visited);
    }

    [Fact]
    public void TraverseForest_MaxDepth_AppliesAcrossAllRoots()
    {
        var forest = BuildSampleForest();
        var visited = new List<string>();

        CompositeTraverser.TraverseForest(
            forest,
            (node, _, _) => visited.Add(node.Name),
            new CompositeTraversalOptions<ITestNode> { MaxDepth = 1 });

        Assert.Equal(new[] { "root-a", "child-a", "root-b", "child-b" }, visited);
    }

    [Fact]
    public async Task TraverseForestAsync_CancellationRequested_StopsEarly()
    {
        var forest = BuildSampleForest();
        using var cts = new CancellationTokenSource();
        var visited = new List<string>();

        await CompositeTraverser.TraverseForestAsync(
            forest,
            (node, _, _, _) =>
            {
                visited.Add(node.Name);
                if (node.Name == "child-a")
                {
                    cts.Cancel();
                }

                return default;
            },
            cancellationToken: cts.Token);

        Assert.Equal(new[] { "root-a", "child-a" }, visited);
    }

    private static IReadOnlyList<ITestNode> BuildSampleForest()
    {
        var rootA = new TestNode("root-a");
        var childA = new TestNode("child-a");
        var grandchildA = new TestNode("grandchild-a");
        var rootB = new TestNode("root-b");
        var childB = new TestNode("child-b");

        childA.SetChildren(new[] { grandchildA });
        rootA.SetChildren(new[] { childA });
        rootB.SetChildren(new[] { childB });

        return new ITestNode[] { rootA, rootB };
    }

    private static ITestNode BuildSampleTree()
    {
        var root = new TestNode("root");
        var childA = new TestNode("child-a");
        var childB = new TestNode("child-b");
        var grandchild = new TestNode("grandchild");

        childA.SetChildren(new[] { grandchild });
        root.SetChildren(new[] { childA, childB });
        return root;
    }

    // ─── Parallel sync tests ──────────────────────────────────────────

    [Fact]
    public void TraverseParallel_DepthFirstPreOrder_VisitsAllNodes()
    {
        var root = BuildSampleTree();
        var visited = new ConcurrentBag<string>();

        CompositeTraverser.TraverseParallel(root, (node, _, _) => visited.Add(node.Name));

        Assert.Equal(4, visited.Count);
        Assert.Contains("root", visited);
        Assert.Contains("child-a", visited);
        Assert.Contains("child-b", visited);
        Assert.Contains("grandchild", visited);
    }

    [Fact]
    public void TraverseParallel_BreadthFirst_VisitsAllNodes()
    {
        var root = BuildSampleTree();
        var visited = new ConcurrentBag<string>();

        CompositeTraverser.TraverseParallel(
            root,
            (node, _, _) => visited.Add(node.Name),
            new CompositeTraversalOptions<ITestNode> { Order = CompositeTraversalOrder.BreadthFirst });

        Assert.Equal(4, visited.Count);
        Assert.Contains("root", visited);
        Assert.Contains("child-a", visited);
        Assert.Contains("child-b", visited);
        Assert.Contains("grandchild", visited);
    }

    [Fact]
    public void TraverseParallel_DepthFirstPostOrder_VisitsAllNodes()
    {
        var root = BuildSampleTree();
        var visited = new ConcurrentBag<string>();

        CompositeTraverser.TraverseParallel(
            root,
            (node, _, _) => visited.Add(node.Name),
            new CompositeTraversalOptions<ITestNode> { Order = CompositeTraversalOrder.DepthFirstPostOrder });

        Assert.Equal(4, visited.Count);
        Assert.Contains("root", visited);
        Assert.Contains("child-a", visited);
        Assert.Contains("child-b", visited);
        Assert.Contains("grandchild", visited);
    }

    [Fact]
    public void TraverseParallel_RespectsMaxDepth()
    {
        var root = BuildSampleTree();
        var visited = new ConcurrentBag<string>();

        CompositeTraverser.TraverseParallel(
            root,
            (node, _, _) => visited.Add(node.Name),
            new CompositeTraversalOptions<ITestNode> { MaxDepth = 1 });

        Assert.Equal(3, visited.Count);
        Assert.Contains("root", visited);
        Assert.Contains("child-a", visited);
        Assert.Contains("child-b", visited);
        Assert.DoesNotContain("grandchild", visited);
    }

    [Fact]
    public void TraverseParallel_RespectsVisitLeavesOnly()
    {
        var root = BuildSampleTree();
        var visited = new ConcurrentBag<string>();

        CompositeTraverser.TraverseParallel(
            root,
            (node, _, _) => visited.Add(node.Name),
            new CompositeTraversalOptions<ITestNode> { VisitLeavesOnly = true });

        Assert.Equal(2, visited.Count);
        Assert.Contains("child-b", visited);
        Assert.Contains("grandchild", visited);
    }

    [Fact]
    public void TraverseParallel_RespectsShouldSkipSubtree()
    {
        var root = BuildSampleTree();
        var visited = new ConcurrentBag<string>();

        CompositeTraverser.TraverseParallel(
            root,
            (node, _, _) => visited.Add(node.Name),
            new CompositeTraversalOptions<ITestNode> { ShouldSkipSubtree = n => n.Name == "child-a" });

        Assert.Equal(2, visited.Count);
        Assert.Contains("root", visited);
        Assert.Contains("child-b", visited);
        Assert.DoesNotContain("child-a", visited);
        Assert.DoesNotContain("grandchild", visited);
    }

    [Fact]
    public void TraverseParallel_AggregatesExceptions()
    {
        var root = BuildSampleTree();

        var ex = Assert.Throws<AggregateException>(() =>
            CompositeTraverser.TraverseParallel(root, (node, _, _) =>
            {
                if (node.Name == "child-a") throw new InvalidOperationException("fail-a");
                if (node.Name == "child-b") throw new InvalidOperationException("fail-b");
            }));

        Assert.Equal(2, ex.InnerExceptions.Count);
    }

    [Fact]
    public void TraverseForestParallel_VisitsAllNodes()
    {
        var forest = BuildSampleForest();
        var visited = new ConcurrentBag<string>();

        CompositeTraverser.TraverseForestParallel(forest, (node, _, _) => visited.Add(node.Name));

        Assert.Equal(5, visited.Count);
        Assert.Contains("root-a", visited);
        Assert.Contains("root-b", visited);
        Assert.Contains("child-a", visited);
        Assert.Contains("child-b", visited);
        Assert.Contains("grandchild-a", visited);
    }

    // ─── Parallel async tests ─────────────────────────────────────────

    [Fact]
    public async Task TraverseParallelAsync_DepthFirstPreOrder_VisitsAllNodes()
    {
        var root = BuildSampleTree();
        var visited = new ConcurrentBag<string>();

        await CompositeTraverser.TraverseParallelAsync(
            root,
            (node, _, _, _) => { visited.Add(node.Name); return default; });

        Assert.Equal(4, visited.Count);
        Assert.Contains("root", visited);
        Assert.Contains("child-a", visited);
        Assert.Contains("child-b", visited);
        Assert.Contains("grandchild", visited);
    }

    [Fact]
    public async Task TraverseParallelAsync_BreadthFirst_VisitsAllNodes()
    {
        var root = BuildSampleTree();
        var visited = new ConcurrentBag<string>();

        await CompositeTraverser.TraverseParallelAsync(
            root,
            (node, _, _, _) => { visited.Add(node.Name); return default; },
            new CompositeTraversalOptions<ITestNode> { Order = CompositeTraversalOrder.BreadthFirst });

        Assert.Equal(4, visited.Count);
    }

    [Fact]
    public async Task TraverseParallelAsync_DepthFirstPostOrder_VisitsAllNodes()
    {
        var root = BuildSampleTree();
        var visited = new ConcurrentBag<string>();

        await CompositeTraverser.TraverseParallelAsync(
            root,
            (node, _, _, _) => { visited.Add(node.Name); return default; },
            new CompositeTraversalOptions<ITestNode> { Order = CompositeTraversalOrder.DepthFirstPostOrder });

        Assert.Equal(4, visited.Count);
    }

    [Fact]
    public async Task TraverseParallelAsync_RespectsMaxDepth()
    {
        var root = BuildSampleTree();
        var visited = new ConcurrentBag<string>();

        await CompositeTraverser.TraverseParallelAsync(
            root,
            (node, _, _, _) => { visited.Add(node.Name); return default; },
            new CompositeTraversalOptions<ITestNode> { MaxDepth = 1 });

        Assert.Equal(3, visited.Count);
        Assert.DoesNotContain("grandchild", visited);
    }

    [Fact]
    public async Task TraverseParallelAsync_AggregatesExceptions()
    {
        var root = BuildSampleTree();

        var ex = await Assert.ThrowsAsync<AggregateException>(async () =>
            await CompositeTraverser.TraverseParallelAsync(
                root,
                (node, _, _, _) =>
                {
                    if (node.Name == "child-a")
                    {
                        throw new InvalidOperationException("fail-a");
                    }
                    return default;
                }));

        Assert.Single(ex.InnerExceptions);
    }

    [Fact]
    public async Task TraverseParallelAsync_RespectsCancellation()
    {
        var root = BuildSampleTree();
        using var cts = new CancellationTokenSource();
        var visited = new ConcurrentBag<string>();

        await CompositeTraverser.TraverseParallelAsync(
            root,
            (node, _, _, ct) =>
            {
                visited.Add(node.Name);
                cts.Cancel();
                ct.ThrowIfCancellationRequested();
                return default;
            },
            cancellationToken: cts.Token);

        // At least one node visited before cancellation; no more after.
        Assert.True(visited.Count >= 1);
    }

    [Fact]
    public async Task TraverseForestParallelAsync_VisitsAllNodes()
    {
        var forest = BuildSampleForest();
        var visited = new ConcurrentBag<string>();

        await CompositeTraverser.TraverseForestParallelAsync(
            forest,
            (node, _, _, _) => { visited.Add(node.Name); return default; });

        Assert.Equal(5, visited.Count);
    }

    [Fact]
    public async Task TraverseParallelAsync_WithMaxDegreeOfParallelism1_VisitsAllNodes()
    {
        var root = BuildSampleTree();
        var visited = new ConcurrentBag<string>();

        await CompositeTraverser.TraverseParallelAsync(
            root,
            (node, _, _, _) => { visited.Add(node.Name); return default; },
            new CompositeTraversalOptions<ITestNode> { MaxDegreeOfParallelism = 1 });

        Assert.Equal(4, visited.Count);
    }

    // ─── Boundary + validation tests ──────────────────────────────────

    [Fact]
    public void TraverseParallel_MaxParallelDepth0_VisitsAllNodes()
    {
        var root = BuildSampleTree();
        var visited = new ConcurrentBag<string>();

        CompositeTraverser.TraverseParallel(
            root,
            (node, _, _) => visited.Add(node.Name),
            new CompositeTraversalOptions<ITestNode> { MaxParallelDepth = 0 });

        Assert.Equal(4, visited.Count);
        Assert.Contains("root", visited);
        Assert.Contains("child-a", visited);
        Assert.Contains("child-b", visited);
        Assert.Contains("grandchild", visited);
    }

    [Fact]
    public void TraverseParallel_WithMaxDegreeOfParallelism1_VisitsAllNodes()
    {
        var root = BuildSampleTree();
        var visited = new ConcurrentBag<string>();

        CompositeTraverser.TraverseParallel(
            root,
            (node, _, _) => visited.Add(node.Name),
            new CompositeTraversalOptions<ITestNode> { MaxDegreeOfParallelism = 1 });

        Assert.Equal(4, visited.Count);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void TraverseParallel_InvalidMaxDegreeOfParallelism_Throws(int invalidDop)
    {
        var root = BuildSampleTree();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CompositeTraverser.TraverseParallel(
                root,
                (_, _, _) => { },
                new CompositeTraversalOptions<ITestNode> { MaxDegreeOfParallelism = invalidDop }));
    }

    [Fact]
    public void TraverseParallel_NegativeMaxParallelDepth_Throws()
    {
        var root = BuildSampleTree();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CompositeTraverser.TraverseParallel(
                root,
                (_, _, _) => { },
                new CompositeTraversalOptions<ITestNode> { MaxParallelDepth = -1 }));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task TraverseParallelAsync_InvalidMaxDegreeOfParallelism_Throws(int invalidDop)
    {
        var root = BuildSampleTree();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            CompositeTraverser.TraverseParallelAsync(
                root,
                (_, _, _, _) => default,
                new CompositeTraversalOptions<ITestNode> { MaxDegreeOfParallelism = invalidDop })
                .AsTask());
    }
}
