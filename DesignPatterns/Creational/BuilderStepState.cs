namespace DesignPatterns.Creational;

/// <summary>
/// Type-state marker types used by generated step builders to prove required-step completeness.
/// </summary>
/// <remarks>
/// Each required step corresponds to a type parameter on <c>{Holder}Builder</c> that flips from
/// <see cref="NotSet"/> to <see cref="Set"/> when the step is applied. These types are never
/// instantiated at runtime.
/// </remarks>
public static class BuilderStepState
{
    /// <summary>
    /// Phantom type indicating a required builder step has not been applied.
    /// </summary>
    public sealed class NotSet
    {
        private NotSet()
        {
        }
    }

    /// <summary>
    /// Phantom type indicating a required builder step has been applied.
    /// </summary>
    public sealed class Set
    {
        private Set()
        {
        }
    }
}
