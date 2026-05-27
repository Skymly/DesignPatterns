using DesignPatterns.Structural;

namespace Composite.Sample;

[CompositePart<IMenuNode>("settings", ParentKey = "root", Order = 10)]
public sealed class SettingsMenu : IMenuNode, ICompositeBuildable<IMenuNode>
{
    private IReadOnlyList<IMenuNode> _children = Array.Empty<IMenuNode>();

    public string Title => "Settings";

    public IReadOnlyList<IMenuNode> Children => _children;

    public void SetChildren(IReadOnlyList<IMenuNode> children) =>
        _children = children.ToList();
}
