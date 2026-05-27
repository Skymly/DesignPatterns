using DesignPatterns.Structural;

namespace Composite.Sample;

[CompositePart<IMenuNode>("root")]
public sealed class HomeMenu : IMenuNode, ICompositeBuildable<IMenuNode>
{
    private IReadOnlyList<IMenuNode> _children = Array.Empty<IMenuNode>();

    public string Title => "Home";

    public IReadOnlyList<IMenuNode> Children => _children;

    public void SetChildren(IReadOnlyList<IMenuNode> children) =>
        _children = children.ToList();
}
