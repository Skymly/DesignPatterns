using DesignPatterns.Structural;

namespace DesignPatterns.Tests.Integration.Composite;

public interface IIntegrationMenuNode : ICompositeNode<IIntegrationMenuNode>
{
    string Title { get; }
}

[CompositePart<IIntegrationMenuNode>("root")]
public sealed class IntegrationRootMenu : IIntegrationMenuNode, ICompositeBuildable<IIntegrationMenuNode>
{
    private IReadOnlyList<IIntegrationMenuNode> _children = Array.Empty<IIntegrationMenuNode>();

    public string Title => "Root";

    public IReadOnlyList<IIntegrationMenuNode> Children => _children;

    public void SetChildren(IReadOnlyList<IIntegrationMenuNode> children) =>
        _children = children.ToList();
}

[CompositePart<IIntegrationMenuNode>("child", ParentKey = "root", Order = 0)]
public sealed class IntegrationChildMenu : IIntegrationMenuNode, ICompositeBuildable<IIntegrationMenuNode>
{
    private IReadOnlyList<IIntegrationMenuNode> _children = Array.Empty<IIntegrationMenuNode>();

    public string Title => "Child";

    public IReadOnlyList<IIntegrationMenuNode> Children => _children;

    public void SetChildren(IReadOnlyList<IIntegrationMenuNode> children) =>
        _children = children.ToList();
}

public sealed class CompositeCatalogIntegrationTests
{
    [Fact]
    public void GeneratedCatalog_BuildRootAssemblesTree()
    {
        var root = IntegrationMenuNodeCompositeCatalog.BuildRoot();

        Assert.Equal("Root", root.Title);
        Assert.Single(root.Children);
        Assert.Equal("Child", root.Children[0].Title);
    }

    [Fact]
    public void GeneratedCatalog_TraverserVisitsNodesInOrder()
    {
        var root = IntegrationMenuNodeCompositeCatalog.BuildRoot();
        var visited = new List<string>();

        CompositeTraverser.Traverse(root, (node, _, _) => visited.Add(node.Title));

        Assert.Equal(new[] { "Root", "Child" }, visited);
    }

    [Fact]
    public void GeneratedKeys_ExposeCatalogKeyConstants()
    {
        Assert.Equal("root", IntegrationMenuNodeCompositeKeys.Root);
        Assert.Equal("child", IntegrationMenuNodeCompositeKeys.Child);
    }
}
