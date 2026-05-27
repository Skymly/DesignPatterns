using System.Collections.Generic;

namespace DesignPatterns.Structural;

/// <summary>
/// Allows a composite node to receive assembled children during catalog-based tree construction.
/// </summary>
/// <typeparam name="TNode">The composite contract type.</typeparam>
public interface ICompositeBuildable<TNode>
    where TNode : ICompositeNode<TNode>
{
    /// <summary>
    /// Sets the child nodes. Called once during assembly; implementations should treat subsequent calls as invalid.
    /// </summary>
    /// <param name="children">Ordered child nodes. Empty for leaves.</param>
    void SetChildren(IReadOnlyList<TNode> children);
}
