using DesignPatterns.Structural;

namespace Composite.Sample;

[CompositePart<IMenuNode>("profile", ParentKey = "root", Order = 20)]
public sealed class ProfileMenu : IMenuNode, ICompositeBuildable<IMenuNode>
{
    private IReadOnlyList<IMenuNode> _children = Array.Empty<IMenuNode>();

    public string Title => "Profile";

    public IReadOnlyList<IMenuNode> Children => _children;

    public void SetChildren(IReadOnlyList<IMenuNode> children) =>
        _children = children.ToList();
}
