using DesignPatterns.Structural;

namespace DesignPatterns.Tests.Structural;

public sealed class CompositeCatalogAssemblerTests
{
    private interface ITestNode : ICompositeNode<ITestNode>
    {
        string Name { get; }
    }

    private sealed class RootCatalogNode : ITestNode, ICompositeBuildable<ITestNode>
    {
        private IReadOnlyList<ITestNode> _children = Array.Empty<ITestNode>();

        public string Name => "root";

        public IReadOnlyList<ITestNode> Children => _children;

        public void SetChildren(IReadOnlyList<ITestNode> children) =>
            _children = children.ToList();
    }

    private sealed class SecondaryRootNode : ITestNode, ICompositeBuildable<ITestNode>
    {
        private IReadOnlyList<ITestNode> _children = Array.Empty<ITestNode>();

        public string Name => "root-b";

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
    public void AssembleForest_MultipleRoots_BuildsOrderedForest()
    {
        var entries = new[]
        {
            new CompositeCatalogEntry<ITestNode>("root-a", null, 1, typeof(RootCatalogNode)),
            new CompositeCatalogEntry<ITestNode>("root-b", null, 0, typeof(SecondaryRootNode)),
            new CompositeCatalogEntry<ITestNode>("child-a", "root-a", 0, typeof(NodeA)),
            new CompositeCatalogEntry<ITestNode>("child-b", "root-b", 0, typeof(NodeB)),
        };

        var forest = CompositeCatalogAssembler.AssembleForest(entries);

        Assert.Equal(2, forest.Count);
        Assert.Equal("root-b", forest[0].Name);
        Assert.Equal("root", forest[1].Name);
        Assert.Single(forest[0].Children);
        Assert.Equal("b", forest[0].Children[0].Name);
        Assert.Single(forest[1].Children);
        Assert.Equal("a", forest[1].Children[0].Name);
    }

    [Fact]
    public void AssembleForest_SingleRoot_ReturnsOneElementForest()
    {
        var entries = new[]
        {
            new CompositeCatalogEntry<ITestNode>("root", null, 0, typeof(RootCatalogNode)),
            new CompositeCatalogEntry<ITestNode>("a", "root", 0, typeof(NodeA)),
        };

        var forest = CompositeCatalogAssembler.AssembleForest(entries);

        Assert.Single(forest);
        Assert.Equal("root", forest[0].Name);
        Assert.Single(forest[0].Children);
        Assert.Equal("a", forest[0].Children[0].Name);
    }

    [Fact]
    public void AssembleForest_EmptyCatalog_ReturnsEmptyList()
    {
        var forest = CompositeCatalogAssembler.AssembleForest(Array.Empty<CompositeCatalogEntry<ITestNode>>());

        Assert.Empty(forest);
    }

    [Fact]
    public void AssembleForest_NullEntries_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => CompositeCatalogAssembler.AssembleForest<ITestNode>(null!));
    }

    [Fact]
    public void Assemble_MultipleRoots_StillThrows()
    {
        var entries = new[]
        {
            new CompositeCatalogEntry<ITestNode>("root-a", null, 0, typeof(RootCatalogNode)),
            new CompositeCatalogEntry<ITestNode>("root-b", null, 0, typeof(SecondaryRootNode)),
        };

        var ex = Assert.Throws<CompositeAssemblyException>(() => CompositeCatalogAssembler.Assemble(entries));
        Assert.Contains("2 root entries", ex.Message);
    }
}
