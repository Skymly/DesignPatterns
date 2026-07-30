using System;

namespace DesignPatterns.Creational;

/// <summary>
/// Declares a construction step on a <see cref="GenerateBuilderAttribute"/> schema holder.
/// </summary>
/// <remarks>
/// Step methods are signature/schema only; the generated <c>{Holder}Builder</c> stores step
/// values and enforces at-most-once application. Required-step completeness is proven with
/// type-state markers; mutex groups and <see cref="After"/>/<see cref="Before"/> constraints
/// are enforced by generator diagnostics.
/// </remarks>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class BuilderStepAttribute : Attribute
{
    /// <summary>
    /// When <see langword="true"/> (default), the step must be applied before <c>Build()</c>
    /// is callable. When <see langword="false"/>, the step may be omitted; an unset optional
    /// value is passed to the assemble method as a null-compatible argument.
    /// </summary>
    public bool Required { get; set; } = true;

    /// <summary>
    /// Optional mutex group name. At most one step in the same group may be applied;
    /// conflicts are reported as generator diagnostics.
    /// </summary>
    public string? MutexGroup { get; set; }

    /// <summary>
    /// Optional name of another step method that must be applied before this step.
    /// Partial-order violations are reported as generator diagnostics.
    /// Prefer <c>nameof</c> of the sibling step method.
    /// </summary>
    public string? After { get; set; }

    /// <summary>
    /// Optional name of another step method that must be applied after this step.
    /// Partial-order violations are reported as generator diagnostics.
    /// Prefer <c>nameof</c> of the sibling step method.
    /// </summary>
    public string? Before { get; set; }
}
