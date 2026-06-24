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

    [Fact]
    public void GeneratedVisitor_VisitDispatchesToCorrectOverload()
    {
        var root = IntegrationMenuNodeCompositeCatalog.BuildRoot();
        var visitor = new TestMenuVisitor();

        root.AcceptVisitor(visitor);
        root.Children[0].AcceptVisitor(visitor);

        Assert.Equal(2, visitor.Visited.Count);
        Assert.Equal("Root", visitor.Visited[0]);
        Assert.Equal("Child", visitor.Visited[1]);
    }

    [Fact]
    public async Task GeneratedAsyncVisitor_VisitAsyncDispatchesToCorrectOverload()
    {
        var root = IntegrationMenuNodeCompositeCatalog.BuildRoot();
        var visitor = new TestAsyncMenuVisitor();

        await root.AcceptVisitorAsync(visitor, CancellationToken.None);
        await root.Children[0].AcceptVisitorAsync(visitor, CancellationToken.None);

        Assert.Equal(2, visitor.Visited.Count);
        Assert.Equal("Root", visitor.Visited[0]);
        Assert.Equal("Child", visitor.Visited[1]);
    }

    [Fact]
    public void GeneratedGenericVisitor_AcceptVisitorReturnsResult()
    {
        var root = IntegrationMenuNodeCompositeCatalog.BuildRoot();
        var visitor = new TestGenericMenuVisitor();

        var rootResult = root.AcceptVisitor<string>(visitor);
        var childResult = root.Children[0].AcceptVisitor<string>(visitor);

        Assert.Equal("Root:Root", rootResult);
        Assert.Equal("Child:Child", childResult);
    }
}

internal sealed class TestMenuVisitor : IIntegrationMenuNodeNodeVisitor
{
    public List<string> Visited { get; } = new();

    public void Visit(IntegrationRootMenu node) => Visited.Add(node.Title);
    public void Visit(IntegrationChildMenu node) => Visited.Add(node.Title);
}

internal sealed class TestAsyncMenuVisitor : IIntegrationMenuNodeNodeAsyncVisitor
{
    public List<string> Visited { get; } = new();

    public ValueTask VisitAsync(IntegrationRootMenu node, CancellationToken cancellationToken)
    {
        Visited.Add(node.Title);
        return ValueTask.CompletedTask;
    }

    public ValueTask VisitAsync(IntegrationChildMenu node, CancellationToken cancellationToken)
    {
        Visited.Add(node.Title);
        return ValueTask.CompletedTask;
    }
}

internal sealed class TestGenericMenuVisitor : IIntegrationMenuNodeNodeVisitor<string>
{
    public string Visit(IntegrationRootMenu node) => $"{node.Title}:Root";
    public string Visit(IntegrationChildMenu node) => $"{node.Title}:Child";
}
