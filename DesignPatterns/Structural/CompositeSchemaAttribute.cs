using System;

namespace DesignPatterns.Structural;

/// <summary>
/// Declares compile-time tree structure constraints for a composite contract.
/// Apply to the contract interface or base class to enable schema validation.
/// </summary>
/// <remarks>
/// When applied, the source generator validates the composite tree structure at compile time:
/// <see cref="MaxDepth"/> limits tree depth, <see cref="MaxNodes"/> limits total node count.
/// All constraints are opt-in — unmarked contracts behave as before.
/// </remarks>
[AttributeUsage(AttributeTargets.Interface | AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class CompositeSchemaAttribute : Attribute
{
    /// <summary>
    /// Maximum tree depth (root = depth 1). 0 = no limit. Default = 0.
    /// </summary>
    public int MaxDepth { get; set; }

    /// <summary>
    /// Maximum total node count across all roots. 0 = no limit. Default = 0.
    /// </summary>
    public int MaxNodes { get; set; }
}
