namespace DesignPatterns.Structural;

/// <summary>
/// Traversal order for <see cref="CompositeTraverser"/>.
/// </summary>
public enum CompositeTraversalOrder
{
    /// <summary>
    /// Visit node before its descendants.
    /// </summary>
    DepthFirstPreOrder,

    /// <summary>
    /// Visit node after its descendants.
    /// </summary>
    DepthFirstPostOrder,

    /// <summary>
    /// Visit nodes level by level.
    /// </summary>
    BreadthFirst,
}
