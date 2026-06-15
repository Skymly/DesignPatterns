using System;

namespace DesignPatterns.Behavioral;

/// <summary>
/// Marks a partial static holder for a generated state transition table.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class StateMachineAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StateMachineAttribute"/> class.
    /// </summary>
    /// <param name="stateType">State enum type.</param>
    /// <param name="triggerType">Trigger enum type.</param>
    public StateMachineAttribute(Type stateType, Type triggerType)
    {
        StateType = stateType ?? throw new ArgumentNullException(nameof(stateType));
        TriggerType = triggerType ?? throw new ArgumentNullException(nameof(triggerType));
    }

    /// <summary>
    /// State enum type.
    /// </summary>
    public Type StateType { get; }

    /// <summary>
    /// Trigger enum type.
    /// </summary>
    public Type TriggerType { get; }

    /// <summary>
    /// Initial state enum member.
    /// </summary>
    public object Initial { get; set; } = null!;
}
