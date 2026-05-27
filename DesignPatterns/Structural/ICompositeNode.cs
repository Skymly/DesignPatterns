using System.Collections.Generic;

namespace DesignPatterns.Structural;

/// <summary>
/// A node in a composite tree. Leaves and branches are treated uniformly via <see cref="Children"/>.
/// </summary>
/// <typeparam name="TSelf">The concrete node type.</typeparam>
public interface ICompositeNode<TSelf>
    where TSelf : ICompositeNode<TSelf>
{
    /// <summary>
    /// Child nodes. Empty for leaves.
    /// </summary>
    IReadOnlyList<TSelf> Children { get; }
}
