namespace DesignPatterns.Structural;

/// <summary>
/// Extension methods for <see cref="ICompositeNode{TSelf}"/>.
/// </summary>
public static class CompositeNodeExtensions
{
    /// <summary>
    /// Returns <see langword="true"/> when the node has no children.
    /// </summary>
    public static bool IsLeaf<TSelf>(this TSelf node)
        where TSelf : ICompositeNode<TSelf> =>
        node.Children.Count == 0;
}
