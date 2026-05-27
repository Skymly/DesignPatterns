using DesignPatterns.Structural;

namespace Composite.Sample;

public interface IMenuNode : ICompositeNode<IMenuNode>
{
    string Title { get; }
}
