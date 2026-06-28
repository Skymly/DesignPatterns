using System;

namespace DesignPatterns.Behavioral;

/// <summary>
/// Declares a parent-child relationship for a hierarchical state machine.
/// Apply on the holder class, once per child state that has a parent.
/// Requires <see cref="StateMachineAttribute.Hierarchical"/> to be <see langword="true"/>.
/// </summary>
/// <remarks>
/// Single inheritance only: each child state may have at most one parent.
/// Multiple inheritance (multiple parents for the same child) is not supported.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
public sealed class StateParentAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StateParentAttribute"/> class.
    /// </summary>
    /// <param name="child">The child state enum member.</param>
    /// <param name="parent">The parent state enum member. Must differ from <paramref name="child"/>.</param>
    public StateParentAttribute(object child, object parent)
    {
        Child = child ?? throw new ArgumentNullException(nameof(child));
        Parent = parent ?? throw new ArgumentNullException(nameof(parent));
    }

    /// <summary>
    /// The child state enum member.
    /// </summary>
    public object Child { get; }

    /// <summary>
    /// The parent state enum member.
    /// </summary>
    public object Parent { get; }
}
