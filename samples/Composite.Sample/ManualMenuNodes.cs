using DesignPatterns.Structural;

namespace Composite.Sample;

/// <summary>
/// Manual composite nodes (no <see cref="CompositePartAttribute"/>) for <see cref="CompositeTreeBuilder{TNode}"/> demo.
/// </summary>
public sealed class ManualMenuLeaf : IMenuNode
{
    public ManualMenuLeaf(string title) => Title = title;

    public string Title { get; }

    public IReadOnlyList<IMenuNode> Children { get; } = Array.Empty<IMenuNode>();
}

public sealed class ManualMenuBranch : IMenuNode, ICompositeBuildable<IMenuNode>
{
    private IReadOnlyList<IMenuNode> _children = Array.Empty<IMenuNode>();

    public ManualMenuBranch(string title) => Title = title;

    public string Title { get; }

    public IReadOnlyList<IMenuNode> Children => _children;

    public void SetChildren(IReadOnlyList<IMenuNode> children) =>
        _children = children.ToList();
}
